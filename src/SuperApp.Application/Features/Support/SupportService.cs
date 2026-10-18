using FluentValidation;
using SuperApp.Application.Abstractions.Email;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Support;

/// <summary>
/// Support tickets (API §6.4, FR-SUP-01/02/04): customers, partners, sellers and factories open a ticket that is a live
/// chat with the Super App team. Opening sends the auto-reply ("we reply within 24 hours") in the chat and by email.
/// </summary>
public interface ISupportService
{
    Task<TicketDto> OpenAsync(OpenTicketRequest request, CancellationToken ct);

    Task<IReadOnlyList<TicketDto>> MineAsync(CancellationToken ct);

    Task<TicketDto> GetAsync(string code, CancellationToken ct);

    Task<TicketMessageDto> ReplyAsync(string code, TicketMessageRequest request, IReadOnlyList<UploadedFile> files, CancellationToken ct);

    Task<TicketDto> CloseAsync(string code, CancellationToken ct);

    Task ReadAsync(string code, CancellationToken ct);
}

public sealed class SupportService(
    ISupportRepository repository,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    INotifier notifier,
    IEmailService email,
    ISupportLive live,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<OpenTicketRequest> openRules,
    IValidator<TicketMessageRequest> messageRules) : ISupportService
{
    public const string AutoReply = "Thanks for reaching out — the Super App team replies within 24 hours. You can add details or photos here meanwhile.";

    private Guid UserId => currentUser.UserId ?? throw new UnauthorizedException();

    public async Task<TicketDto> OpenAsync(OpenTicketRequest request, CancellationToken ct)
    {
        await openRules.ValidateAndThrowAsync(request, ct);
        var bookingId = request.BookingCode is { Length: > 0 } b ? await repository.BookingIdAsync(b, UserId, ct) ?? throw new NotFoundException("Booking not found.", "booking") : (long?)null;
        var orderId = request.OrderCode is { Length: > 0 } o ? await repository.OrderIdAsync(o, UserId, ct) ?? throw new NotFoundException("Order not found.", "order") : (long?)null;
        var dispute = request.DisputeAmount is > 0;
        var ticket = new Ticket
        {
            Code = await repository.NewCodeAsync(ct), OpenedById = UserId, Topic = Enum.Parse<TicketTopic>(request.Topic, true), Subject = request.Subject.Trim(),
            OpenedFrom = Enum.TryParse<TicketOrigin>(request.OpenedFrom, true, out var from) ? from : TicketOrigin.Site, BookingId = bookingId, OrderId = orderId,
            IsDispute = dispute, DisputeAmount = dispute ? request.DisputeAmount : null, AgainstUserId = dispute ? await repository.CounterpartyAsync(bookingId, orderId, ct) : null,
            Priority = dispute ? TicketPriority.High : TicketPriority.Normal, Status = TicketStatus.WaitingForTeam,
        };
        repository.Add(ticket);
        repository.Add(new TicketMessage { Ticket = ticket, SenderId = UserId, Body = request.Message.Trim() });
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(ticket, ct);
        return (await repository.TicketDtoAsync(ticket.Code, UserId, false, ct))!;
    }

    /// <summary>The auto-reply by email, the team told, and the other side of a dispute told.</summary>
    private async Task TellAsync(Ticket ticket, CancellationToken ct)
    {
        var owner = await repository.OwnerAsync(UserId, ct);
        await email.SendAsync(new EmailMessage(owner.Email, "ticket-opened", new Dictionary<string, string> { ["name"] = owner.Name, ["code"] = ticket.Code, ["subject"] = ticket.Subject },
            owner.Language, owner.UserId), ct);
        await notifier.NotifyStaffAsync("support.read", new NotificationMessage(NotificationCategory.Support, $"New ticket {ticket.Code}", ticket.Subject,
            $"#/admin/support?ticket={ticket.Code}", ticket.IsDispute ? NotificationTone.Warning : NotificationTone.Info), ct);
        if (ticket.AgainstUserId is { } other)
        {
            await notifier.NotifyUserAsync(other, new NotificationMessage(NotificationCategory.Support, $"A dispute was opened ({ticket.Code})",
                "The Super App team will look at it and contact you.", "#/help", NotificationTone.Warning), ct);
        }
    }

    public Task<IReadOnlyList<TicketDto>> MineAsync(CancellationToken ct) => repository.MineAsync(UserId, ct);

    public async Task<TicketDto> GetAsync(string code, CancellationToken ct) =>
        await repository.TicketDtoAsync(code.ToUpperInvariant(), UserId, false, ct) is { } t && await OwnAsync(code, ct) is not null ? t : throw new NotFoundException("Ticket not found.");

    public async Task<TicketMessageDto> ReplyAsync(string code, TicketMessageRequest request, IReadOnlyList<UploadedFile> files, CancellationToken ct)
    {
        await messageRules.ValidateAndThrowAsync(request, ct);
        var ticket = await OwnAsync(code, ct) ?? throw new NotFoundException("Ticket not found.");
        if (ticket.Status is TicketStatus.Closed or TicketStatus.Resolved)
        {
            throw new BusinessRuleException("This ticket is closed. Open a new one if you still need help.", "closed");
        }
        if (files.Count > 4)
        {
            throw new BusinessRuleException("Up to 4 photos or files per message.", "too_many_files");
        }
        var message = new TicketMessage { TicketId = ticket.Id, SenderId = UserId, Body = request.Body.Trim() };
        repository.Add(message);
        foreach (var file in files)
        {
            repository.Add(new TicketAttachment { Message = message, FileId = await storage.SaveAsync(file, UploadKind.Document, UserId, "support", ct) });
        }
        ticket.Status = TicketStatus.WaitingForTeam;
        await unitOfWork.SaveChangesAsync(ct);
        var dto = (await repository.TicketDtoAsync(ticket.Code, UserId, false, ct))!.Messages!.Single(m => m.Id == message.Id);
        await live.PublishAsync(ticket.Code, "message", dto, ct);
        return dto;
    }

    public async Task<TicketDto> CloseAsync(string code, CancellationToken ct)
    {
        var ticket = await OwnAsync(code, ct) ?? throw new NotFoundException("Ticket not found.");
        ticket.Status = TicketStatus.Closed;
        ticket.ResolvedAt ??= clock.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        await live.PublishAsync(ticket.Code, "statusChanged", new { status = ticket.Status.ToString() }, ct);
        return (await repository.TicketDtoAsync(ticket.Code, UserId, false, ct))!;
    }

    public async Task ReadAsync(string code, CancellationToken ct)
    {
        var ticket = await OwnAsync(code, ct) ?? throw new NotFoundException("Ticket not found.");
        if (await repository.MarkReadAsync(ticket.Id, byTeam: false, clock.UtcNow, ct) > 0)
        {
            await live.PublishAsync(ticket.Code, "read", new { by = "customer" }, ct);
        }
    }

    private async Task<Ticket?> OwnAsync(string code, CancellationToken ct) =>
        await repository.TicketAsync(code.ToUpperInvariant(), ct) is { } t && t.OpenedById == UserId ? t : null;
}

public sealed class OpenTicketRequestValidator : AbstractValidator<OpenTicketRequest>
{
    public OpenTicketRequestValidator()
    {
        RuleFor(x => x.Topic).Must(t => Enum.TryParse<TicketTopic>(t, true, out _)).WithMessage("Choose a topic.");
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Write a short subject.").Length(5, 150);
        RuleFor(x => x.Message).NotEmpty().WithMessage("Tell us what happened.").MaximumLength(4000);
        RuleFor(x => x.OpenedFrom).Must(o => o is null || Enum.TryParse<TicketOrigin>(o, true, out _)).WithMessage("Unknown place.");
        RuleFor(x => x.BookingCode).MaximumLength(20);
        RuleFor(x => x.OrderCode).MaximumLength(20);
        RuleFor(x => x.DisputeAmount).GreaterThan(0).LessThanOrEqualTo(1_000_000).When(x => x.DisputeAmount is not null);
        RuleFor(x => x).Must(x => x.DisputeAmount is null || !string.IsNullOrEmpty(x.BookingCode) || !string.IsNullOrEmpty(x.OrderCode))
            .WithName("disputeAmount").WithMessage("A dispute is about a booking or an order.");
    }
}

public sealed class TicketMessageRequestValidator : AbstractValidator<TicketMessageRequest>
{
    public TicketMessageRequestValidator()
    {
        RuleFor(x => x.Body).NotEmpty().WithMessage("Write a message.").MaximumLength(4000);
    }
}
