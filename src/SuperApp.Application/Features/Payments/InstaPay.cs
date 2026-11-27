using System.Text.Json;
using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Payments;

/// <summary>
/// The owner's InstaPay account, from the server's settings only (never in the code): <c>Payments:InstaPay:Address</c>
/// (name@instapay) and <c>Payments:InstaPay:Link</c> (the ipn.eg link inside the account's QR code).
/// </summary>
public interface IInstaPaySettings
{
    string Address { get; }

    string Link { get; }

    bool Configured { get; }
}

/// <summary>What the payer's page shows: where to send the money, how long the hours are kept, and what they sent us.</summary>
public sealed record InstaPayInfo(string Address, string Link, int PayWithinMinutes, DateTime PayBy, string? Reference, string? Sender, DateTime? SubmittedAt);

/// <summary>{ reference (the InstaPay transaction number), sender (name or phone the money came from) }</summary>
public sealed record InstaPaySubmitRequest(string Reference, string Sender);

/// <summary>{ reason } shown to the payer.</summary>
public sealed record InstaPayRejectRequest(string Reason);

/// <summary>One InstaPay payment for the team: who, how much, for what, and what the payer says they sent.</summary>
public sealed record InstaPayQueueItem(string Code, string Purpose, decimal Amount, string Status, string PayerName, string PayerEmail, string PayerPhone,
    DateTime CreatedAt, string? Reference, string? Sender, DateTime? SubmittedAt, string? Note);

/// <summary>What the payer sent, kept on the payment (in <see cref="Payment.RawWebhookJson"/>, which InstaPay never uses).</summary>
public sealed record InstaPayClaim(string? Reference, string? Sender, DateTime? SubmittedAt, string? Note = null, string? DecidedBy = null)
{
    public static InstaPayClaim Read(Payment p)
    {
        if (string.IsNullOrEmpty(p.RawWebhookJson))
        {
            return new InstaPayClaim(null, null, null);
        }
        try
        {
            return JsonSerializer.Deserialize<InstaPayClaim>(p.RawWebhookJson) ?? new InstaPayClaim(null, null, null);
        }
        catch (JsonException)
        {
            return new InstaPayClaim(null, null, null);
        }
    }

    public string Json() => JsonSerializer.Serialize(this);
}

/// <summary>
/// Manual InstaPay (a stop-gap until Paymob): the payer sends the money to the owner's InstaPay account, then types the
/// transaction number here. Nothing is confirmed by the payer: only a team member with <c>payments.confirm</c>, after
/// seeing the money in the bank app, confirms it — then the booking/order is confirmed and points are given as usual.
/// </summary>
public sealed class InstaPayService(
    IPaymentRepository payments, IPaymentsService settle, ICurrentUser currentUser, IUnitOfWork unitOfWork,
    INotifier notifier, IAuditLogger audit, IClock clock)
{
    public const string Provider = "instapay";

    /// <summary>The payer has this long to send the money and its number; after that the hours/items are freed.</summary>
    public static readonly TimeSpan PayWithin = TimeSpan.FromMinutes(30);

    /// <summary>The payer's own pending InstaPay payment only. Sending it twice just updates the number.</summary>
    public async Task<PaymentStatusDto> SubmitAsync(string code, InstaPaySubmitRequest request, CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        var p = await payments.FindAsync(code, ct);
        if (p is null || p.PayerUserId != me || p.Provider != Provider)
        {
            throw new NotFoundException("Payment not found.");
        }
        if (p.Status != PaymentStatus.Pending)
        {
            throw new BusinessRuleException("This payment is no longer waiting for InstaPay.", "payment_closed");
        }
        if (InstaPayClaim.Read(p).SubmittedAt is null && p.CreatedAt <= clock.UtcNow - PayWithin)
        {
            throw new BusinessRuleException("The time to pay is over. Please book again.", "payment_expired");
        }
        p.RawWebhookJson = new InstaPayClaim(request.Reference.Trim(), request.Sender.Trim(), clock.UtcNow).Json();
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyStaffAsync("payments.confirm", new NotificationMessage(NotificationCategory.Money, "InstaPay payment to check",
            $"{p.Code} · {p.Amount:0} EGP · number {request.Reference.Trim()} · from {request.Sender.Trim()}", "#/admin/instapay", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Site, "payment.instapay_sent", $"Payer says {p.Amount:0.##} EGP were sent by InstaPay for {p.Code}", "Payment", p.Code, ct: ct);
        return await settle.GetAsync(code, ct);
    }

    /// <summary>?status=waiting (sent, to check — default) | unsent | all</summary>
    public Task<IReadOnlyList<InstaPayQueueItem>> QueueAsync(string? status, CancellationToken ct) =>
        payments.InstaPayQueueAsync((status ?? "waiting").Trim().ToLowerInvariant(), ct);

    /// <summary>The money is in the account: the payment is paid and its booking/order goes on as after a card payment.</summary>
    public async Task<InstaPayQueueItem> ConfirmAsync(string code, CancellationToken ct)
    {
        var p = await PendingAsync(code, ct);
        var claim = InstaPayClaim.Read(p);
        p.RawWebhookJson = (claim with { DecidedBy = currentUser.UserId?.ToString(), Note = "confirmed" }).Json();
        p.ProviderRef = claim.Reference ?? p.ProviderRef;
        await settle.SettleAsync(p, paid: true, ct);
        return await ItemAsync(code, ct);
    }

    /// <summary>The money never came (or the amount is wrong): the payment fails and the payer is told why.</summary>
    public async Task<InstaPayQueueItem> RejectAsync(string code, InstaPayRejectRequest request, CancellationToken ct)
    {
        var p = await PendingAsync(code, ct);
        p.RawWebhookJson = (InstaPayClaim.Read(p) with { DecidedBy = currentUser.UserId?.ToString(), Note = request.Reason.Trim() }).Json();
        await settle.SettleAsync(p, paid: false, ct);
        await notifier.NotifyUserAsync(p.PayerUserId!.Value, new NotificationMessage(NotificationCategory.Money, "InstaPay payment not found",
            $"{p.Code} · {p.Amount:0} EGP · {request.Reason.Trim()}", "#/support", NotificationTone.Warning), ct);
        return await ItemAsync(code, ct);
    }

    private async Task<Payment> PendingAsync(string code, CancellationToken ct)
    {
        var p = await payments.FindAsync(code, ct);
        if (p is null || p.Provider != Provider)
        {
            throw new NotFoundException("Payment not found.");
        }
        return p.Status == PaymentStatus.Pending ? p : throw new BusinessRuleException("This payment was already decided.", "payment_closed");
    }

    private async Task<InstaPayQueueItem> ItemAsync(string code, CancellationToken ct) =>
        (await payments.InstaPayQueueAsync("all", ct)).FirstOrDefault(i => i.Code == code) ?? throw new NotFoundException("Payment not found.");
}

public sealed class InstaPaySubmitValidator : AbstractValidator<InstaPaySubmitRequest>
{
    public InstaPaySubmitValidator()
    {
        RuleFor(x => x.Reference).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9 \\-]+$").WithMessage("Use the transaction number from InstaPay.");
        RuleFor(x => x.Sender).NotEmpty().MaximumLength(80);
    }
}

public sealed class InstaPayRejectValidator : AbstractValidator<InstaPayRejectRequest>
{
    public InstaPayRejectValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(200);
}
