using FluentValidation;
using SuperApp.Application.Abstractions.Email;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Support;

/// <summary>
/// The support inbox (API §7.2): read (support.read), reply or leave internal notes (support.reply), assign and set
/// priority (support.assign), resolve, and decide disputes (disputes.decide). Every reply reaches the customer in the chat,
/// as a notification and by email. Dispute refunds are recorded here and paid by the money module (Phase 5).
/// </summary>
public interface ISupportAdminService
{
    Task<PagedResult<TicketDto>> InboxAsync(TicketsQuery query, CancellationToken ct);

    Task<TicketDto> GetAsync(string code, CancellationToken ct);

    Task<TicketMessageDto> ReplyAsync(string code, TeamMessageRequest request, CancellationToken ct);

    Task<TicketDto> AssignAsync(string code, AssignRequest request, CancellationToken ct);

    Task<TicketDto> PriorityAsync(string code, PriorityRequest request, CancellationToken ct);

    Task<TicketDto> ResolveAsync(string code, CancellationToken ct);

    Task<TicketDto> DisputeAsync(string code, DisputeRequest request, CancellationToken ct);
}

public sealed class SupportAdminService(
    ISupportRepository repository,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IEmailService email,
    ISupportLive live,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<TeamMessageRequest> replyRules,
    IValidator<DisputeRequest> disputeRules,
    Money.ILedger ledger) : ISupportAdminService
{
    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public Task<PagedResult<TicketDto>> InboxAsync(TicketsQuery query, CancellationToken ct) => repository.InboxAsync(query, ct);

    public async Task<TicketDto> GetAsync(string code, CancellationToken ct)
    {
        var ticket = await TicketAsync(code, ct);
        await repository.MarkReadAsync(ticket.Id, byTeam: true, clock.UtcNow, ct);
        return (await repository.TicketDtoAsync(ticket.Code, UserId, true, ct))!;
    }

    public async Task<TicketMessageDto> ReplyAsync(string code, TeamMessageRequest request, CancellationToken ct)
    {
        await replyRules.ValidateAndThrowAsync(request, ct);
        var ticket = await TicketAsync(code, ct);
        var message = new TicketMessage { TicketId = ticket.Id, SenderId = UserId, Body = request.Body.Trim(), IsInternalNote = request.InternalNote };
        repository.Add(message);
        if (!request.InternalNote)
        {
            ticket.FirstResponseAt ??= clock.UtcNow;
            ticket.Status = ticket.Status is TicketStatus.Resolved or TicketStatus.Closed ? ticket.Status : TicketStatus.WaitingForCustomer;
        }
        await unitOfWork.SaveChangesAsync(ct);
        var dto = (await repository.TicketDtoAsync(ticket.Code, UserId, true, ct))!.Messages!.Single(m => m.Id == message.Id);
        if (!request.InternalNote)
        {
            await live.PublishAsync(ticket.Code, "message", dto with { IsMine = false }, ct);
            await TellCustomerAsync(ticket, "The Super App team replied", PreviewOf(dto.Body), ct);
        }
        return dto;
    }

    public async Task<TicketDto> AssignAsync(string code, AssignRequest request, CancellationToken ct)
    {
        var ticket = await TicketAsync(code, ct);
        if (request.UserId is { } to && !await repository.IsStaffAsync(to, ct))
        {
            throw new BadRequestException("Tickets go to Super App team members.", "assignee");
        }
        ticket.AssignedToUserId = request.UserId;
        await unitOfWork.SaveChangesAsync(ct);
        if (request.UserId is { } assignee && assignee != UserId)
        {
            await notifier.NotifyUserAsync(assignee, new NotificationMessage(NotificationCategory.Support, $"Ticket {ticket.Code} is yours", ticket.Subject, $"#/admin/support?ticket={ticket.Code}"), ct);
        }
        await live.PublishAsync(ticket.Code, "assigned", new { assigned = request.UserId is not null }, ct);
        await audit.LogAsync(AuditSystem.Admin, "support.assigned", $"{ticket.Code} assigned", "Ticket", ticket.Code, ct: ct);
        return (await repository.TicketDtoAsync(ticket.Code, UserId, true, ct))!;
    }

    public async Task<TicketDto> PriorityAsync(string code, PriorityRequest request, CancellationToken ct)
    {
        var ticket = await TicketAsync(code, ct);
        ticket.Priority = Enum.TryParse<TicketPriority>(request.Priority, true, out var p) ? p : throw new BadRequestException("Priority: Low, Normal, High or Urgent.", "priority");
        await unitOfWork.SaveChangesAsync(ct);
        return (await repository.TicketDtoAsync(ticket.Code, UserId, true, ct))!;
    }

    public async Task<TicketDto> ResolveAsync(string code, CancellationToken ct)
    {
        var ticket = await TicketAsync(code, ct);
        (ticket.Status, ticket.ResolvedAt) = (TicketStatus.Resolved, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        await live.PublishAsync(ticket.Code, "statusChanged", new { status = ticket.Status.ToString() }, ct);
        await TellCustomerAsync(ticket, $"Ticket {ticket.Code} is resolved", "Reply in the chat if you still need help.", ct);
        await audit.LogAsync(AuditSystem.Admin, "support.resolved", $"{ticket.Code} resolved", "Ticket", ticket.Code, ct: ct);
        return (await repository.TicketDtoAsync(ticket.Code, UserId, true, ct))!;
    }

    /// <summary>Records the decision on the ticket (internal note + both sides told) and pays the refund into the customer's wallet.</summary>
    public async Task<TicketDto> DisputeAsync(string code, DisputeRequest request, CancellationToken ct)
    {
        await disputeRules.ValidateAndThrowAsync(request, ct);
        var ticket = await TicketAsync(code, ct);
        if (!ticket.IsDispute)
        {
            throw new BusinessRuleException("This ticket is not a dispute.", "not_dispute");
        }
        if (ticket.DisputeAmount is { } asked && request.Refund > asked)
        {
            throw new BusinessRuleException($"The refund can't be more than the {asked:0.##} EGP asked for.", "over_dispute_amount");
        }
        var byVenue = string.Equals(request.PaidBy, "Partner", StringComparison.OrdinalIgnoreCase) && ticket.AgainstUserId is not null;
        await ledger.CreditAsync(ticket.OpenedById, request.Refund, byVenue ? ticket.AgainstUserId : null, "Ticket", ticket.Id, ct);
        var decision = request.Refund > 0 ? $"Refund {request.Refund:0.##} EGP to the Super App wallet, paid by {(byVenue ? "the venue" : "Super App")}." : "No refund.";
        repository.Add(new TicketMessage { TicketId = ticket.Id, SenderId = UserId, Body = $"Dispute decided: {decision} {request.Note.Trim()}", IsInternalNote = true });
        (ticket.Status, ticket.ResolvedAt) = (TicketStatus.Resolved, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        await TellCustomerAsync(ticket, $"Dispute {ticket.Code} decided", decision, ct);
        if (ticket.AgainstUserId is { } other)
        {
            await notifier.NotifyUserAsync(other, new NotificationMessage(NotificationCategory.Support, $"Dispute {ticket.Code} decided", decision, "#/help", NotificationTone.Warning), ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "support.dispute", $"{ticket.Code}: {decision}", "Ticket", ticket.Code,
            new { request.Refund, request.PaidBy, request.Note }, ct);
        return (await repository.TicketDtoAsync(ticket.Code, UserId, true, ct))!;
    }

    private async Task TellCustomerAsync(Ticket ticket, string title, string body, CancellationToken ct)
    {
        await notifier.NotifyUserAsync(ticket.OpenedById, new NotificationMessage(NotificationCategory.Support, title, body, $"#/support/{ticket.Code}"), ct);
        var owner = await repository.OwnerAsync(ticket.OpenedById, ct);
        await email.SendAsync(new EmailMessage(owner.Email, "ticket-reply", new Dictionary<string, string>
        {
            ["name"] = owner.Name, ["code"] = ticket.Code, ["title"] = title, ["body"] = body,
        }, owner.Language, owner.UserId), ct);
    }

    private static string PreviewOf(string body) => body.Length <= 140 ? body : body[..137] + "…";

    private async Task<Ticket> TicketAsync(string code, CancellationToken ct) =>
        await repository.TicketAsync(code.ToUpperInvariant(), ct) ?? throw new NotFoundException("Ticket not found.");
}

public sealed class TeamMessageRequestValidator : AbstractValidator<TeamMessageRequest>
{
    public TeamMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().WithMessage("Write a reply.").MaximumLength(4000);
    }
}

public sealed class DisputeRequestValidator : AbstractValidator<DisputeRequest>
{
    public DisputeRequestValidator()
    {
        RuleFor(x => x.Refund).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.PaidBy).Must(p => p is "SuperApp" or "Partner" or "Seller").WithMessage("Paid by: SuperApp, Partner or Seller.");
        RuleFor(x => x.Note).NotEmpty().WithMessage("Explain the decision.").MaximumLength(1000);
    }
}
