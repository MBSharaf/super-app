using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.AdminConsole;

/// <summary>Wallet credit, Super Points and refusals (customers); commission, balance and closing a pitch (partners).</summary>
public sealed partial class AdminPeopleService
{
    public async Task<CustomerDetailDto> CreditAsync(Guid id, CreditRequest request, CancellationToken ct)
    {
        var user = await CustomerEntityAsync(id, ct);
        await limits.EnsureCreditAsync(request.Amount, ct);
        await ledger.CreditAsync(id, request.Amount, null, "AdminCredit", 0, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(id, new NotificationMessage(NotificationCategory.Money, $"{request.Amount:0} EGP added to your wallet",
            request.Reason.Trim(), "#/wallet", NotificationTone.Success), ct);
        await audit.LogAsync(AuditSystem.Admin, "customer.credit", $"Credited {request.Amount:0} EGP to {user.FullName}: {request.Reason.Trim()}", "User", id.ToString(),
            new CreditRequest(request.Amount, request.Reason.Trim()), ct);
        return await CustomerAsync(id, ct);
    }

    public async Task<CustomerDetailDto> PointsAsync(Guid id, PointsRequest request, CancellationToken ct)
    {
        var user = await CustomerEntityAsync(id, ct);
        var balance = await loyalty.BalanceAsync(id, ct);
        if (balance + request.Points < 0)
        {
            throw new BusinessRuleException($"They have {balance} points — you can take away at most that.", "not_enough_points");
        }
        loyalty.Add(new PointEntry { UserId = id, Points = request.Points, Kind = PointKind.AdminAdjustment, Reason = request.Reason.Trim(), CreatedAt = clock.UtcNow });
        await unitOfWork.SaveChangesAsync(ct);
        var sign = request.Points > 0 ? "+" : "−";
        await notifier.NotifyUserAsync(id, new NotificationMessage(NotificationCategory.Money, $"{sign}{Math.Abs(request.Points)} Super Points",
            request.Reason.Trim(), "#/wallet", request.Points > 0 ? NotificationTone.Success : NotificationTone.Info), ct);
        await audit.LogAsync(AuditSystem.Admin, "customer.points", $"{sign}{Math.Abs(request.Points)} Super Points for {user.FullName}: {request.Reason.Trim()}", "User", id.ToString(), request, ct);
        return await CustomerAsync(id, ct);
    }

    public async Task<CustomerDetailDto> ResetRefusalsAsync(Guid id, CancellationToken ct)
    {
        var user = await CustomerEntityAsync(id, ct);
        if (user.CodRefusals == 0)
        {
            throw new ConflictException("This customer has no refused deliveries.", "no_refusals");
        }
        var was = user.CodRefusals;
        user.CodRefusals = 0;
        await unitOfWork.SaveChangesAsync(ct);
        await TellAsync(user, "Your delivery record was cleared", "Cash on delivery is available again.", NotificationTone.Success, ct);
        await audit.LogAsync(AuditSystem.Admin, "customer.refusals_reset", $"Reset {user.FullName}'s refused deliveries (was {was})", "User", id.ToString(), ct: ct);
        return await CustomerAsync(id, ct);
    }

    public async Task<PartnerDetailDto> CommissionAsync(long id, CommissionDealRequest request, CancellationToken ct)
    {
        var partner = await people.PartnerAsync(id, ct) ?? throw new NotFoundException("This partner does not exist.");
        (partner.BookingCommission, partner.StoreCommission) = (request.Bookings, request.Store);
        await unitOfWork.SaveChangesAsync(ct);
        var deal = request.Bookings is null && request.Store is null
            ? "back to the platform rates"
            : $"bookings {(request.Bookings is { } b ? $"{b:0.##}%" : "platform rate")}, store {(request.Store is { } s ? $"{s:0.##}%" : "platform rate")}";
        await notifier.NotifyUserAsync(partner.UserId, new NotificationMessage(NotificationCategory.Money, "Your commission was updated",
            $"{deal} — for bookings made from now on.", "#/dash/finance", NotificationTone.Info), ct);
        await audit.LogAsync(AuditSystem.Admin, "partner.commission", $"{partner.BusinessName}: {deal}{(string.IsNullOrWhiteSpace(request.Note) ? "" : $" — {request.Note.Trim()}")}",
            "Partner", id.ToString(), request, ct);
        return await PartnerAsync(id, ct);
    }

    public async Task<PartnerDetailDto> AdjustBalanceAsync(long id, BalanceAdjustRequest request, CancellationToken ct)
    {
        var partner = await people.PartnerAsync(id, ct) ?? throw new NotFoundException("This partner does not exist.");
        await limits.EnsureAdjustAsync(request.Amount, ct);
        await ledger.AdjustAsync(partner.UserId, WalletType.PartnerBookings, request.Amount, "Adjustment", id, ct);
        await unitOfWork.SaveChangesAsync(ct);
        var text = $"{(request.Amount > 0 ? "+" : "−")}{Math.Abs(request.Amount):0.##} EGP";
        await notifier.NotifyUserAsync(partner.UserId, new NotificationMessage(NotificationCategory.Money, $"Balance adjusted: {text}",
            request.Reason.Trim(), "#/dash/wallet", request.Amount > 0 ? NotificationTone.Success : NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, "partner.balance_adjusted", $"{partner.BusinessName}: {text} — {request.Reason.Trim()}", "Partner", id.ToString(), request, ct);
        return await PartnerAsync(id, ct);
    }

    public async Task<PartnerDetailDto> SetPitchClosedAsync(long pitchId, bool close, string? reason, CancellationToken ct)
    {
        var pitch = await people.PitchAsync(pitchId, ct) ?? throw new NotFoundException("This pitch does not exist.");
        var (from, to) = close ? (PitchStatus.Live, PitchStatus.ClosedByAdmin) : (PitchStatus.ClosedByAdmin, PitchStatus.Live);
        if (pitch.Status != from)
        {
            throw new BusinessRuleException(close ? "Only a live pitch can be closed." : "Only a pitch Super App closed can be reopened here.", "wrong_status");
        }
        pitch.Status = to;
        await unitOfWork.SaveChangesAsync(ct);
        var partner = pitch.Venue.Partner;
        var (title, body) = close ? ($"{pitch.Name} was closed by Super App", reason!.Trim()) : ($"{pitch.Name} is open again", "Players can book it again.");
        await notifier.NotifyUserAsync(partner.UserId, new NotificationMessage(NotificationCategory.Bookings, title, body, "#/dash/pitches",
            close ? NotificationTone.Danger : NotificationTone.Success), ct);
        await audit.LogAsync(AuditSystem.Admin, close ? "pitch.closed_by_admin" : "pitch.reopened_by_admin", $"{title}{(close ? $": {body}" : "")}", "Pitch", pitch.Slug, ct: ct);
        return await PartnerAsync(partner.Id, ct);
    }
}

public sealed class CreditRequestValidator : AbstractValidator<CreditRequest>
{
    public CreditRequestValidator()
    {
        RuleFor(x => x.Amount).InclusiveBetween(5, 5000).WithMessage("Credit between 5 and 5,000 EGP.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Write the reason (they see it).").Length(3, 200);
    }
}

public sealed class PointsRequestValidator : AbstractValidator<PointsRequest>
{
    public PointsRequestValidator()
    {
        RuleFor(x => x.Points).NotEqual(0).WithMessage("Enter the points (not 0).").InclusiveBetween(-100_000, 100_000).WithMessage("At most 100,000 points either way.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Write the reason (they see it).").Length(3, 200);
    }
}

public sealed class CommissionDealRequestValidator : AbstractValidator<CommissionDealRequest>
{
    public CommissionDealRequestValidator()
    {
        RuleFor(x => x.Bookings).InclusiveBetween(0, 50).When(x => x.Bookings is not null).WithMessage("Commission must be between 0% and 50%.");
        RuleFor(x => x.Store).InclusiveBetween(0, 50).When(x => x.Store is not null).WithMessage("Commission must be between 0% and 50%.");
        RuleFor(x => x.Note).MaximumLength(120);
    }
}

public sealed class BalanceAdjustRequestValidator : AbstractValidator<BalanceAdjustRequest>
{
    public BalanceAdjustRequestValidator()
    {
        RuleFor(x => x.Amount).NotEqual(0).WithMessage("Enter the amount (+ adds, − takes away).").InclusiveBetween(-100_000, 100_000).WithMessage("At most 100,000 EGP either way.");
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Write the reason (the partner sees it).").Length(3, 200);
    }
}
