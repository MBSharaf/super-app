using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Payments;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Money;

/// <summary>POST /wallet/top-ups — whole EGP, 50 to 20,000.</summary>
public sealed record TopUpRequest(decimal Amount);

/// <summary>The top-up's payment: Paid (money in the wallet) or Pending with the gateway's page in <c>PaymentUrl</c>.</summary>
public sealed record TopUpDto(string Code, decimal Amount, string Status, decimal Balance, string? PaymentUrl);

/// <summary>
/// Wallet top-ups by card. The simulated gateway pays at once; with Paymob the money arrives when its signed callback
/// confirms the payment (<see cref="WalletTopUpPaymentListener"/>). The Idempotency-Key header makes a retry return the
/// same top-up instead of charging again.
/// </summary>
public interface IWalletTopUpService
{
    Task<TopUpDto> TopUpAsync(TopUpRequest request, string? idempotencyKey, CancellationToken ct);
}

public sealed class WalletTopUpService(
    IPaymentsService payments,
    IPaymentRepository paymentRows,
    IMoneyStore store,
    ILedger ledger,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IValidator<TopUpRequest> rules) : IWalletTopUpService
{
    public async Task<TopUpDto> TopUpAsync(TopUpRequest request, string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new BadRequestException("Send an Idempotency-Key header with the top-up.", "idempotency_key");
        }
        await rules.ValidateAndThrowAsync(request, ct);
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        var key = KeyOf(me, idempotencyKey);
        if (await paymentRows.FindByKeyAsync(key, ct) is { } repeated)
        {
            return await ToDtoAsync(me, repeated, null, ct);
        }
        var started = await payments.StartAsync(new PaymentRequest(PaymentPurpose.WalletTopUp, request.Amount, me, "Super App wallet top-up", "#/wallet",
            IdempotencyKey: key), ct);
        await unitOfWork.SaveChangesAsync(ct);
        if (started.RedirectUrl is null)
        {
            await CreditAsync(ledger, unitOfWork, notifier, started.Payment, ct);
        }
        await audit.LogAsync(AuditSystem.Site, "wallet.top_up", $"Wallet top-up {started.Payment.Code} for {request.Amount:0} EGP", "Payment", started.Payment.Code, ct: ct);
        return await ToDtoAsync(me, started.Payment, started.RedirectUrl, ct);
    }

    /// <summary>Money to the wallet, a receipt and a notification — once per payment (simulated now, or the gateway's callback).</summary>
    internal static async Task CreditAsync(ILedger ledger, IUnitOfWork unitOfWork, INotifier notifier, Payment payment, CancellationToken ct)
    {
        if (!await ledger.TopUpAsync(payment, ct))
        {
            return;
        }
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(payment.PayerUserId!.Value, new NotificationMessage(NotificationCategory.Money, $"{payment.Amount:0} EGP added to your wallet",
            "Your top-up is in. The receipt is in Payments & receipts.", "#/wallet", NotificationTone.Success), ct);
    }

    /// <summary>The key is the person's own: two people sending the same header never meet (64 hex characters).</summary>
    private static string KeyOf(Guid me, string idempotencyKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"topup|{me:N}|{idempotencyKey.Trim()}"))).ToLowerInvariant();

    private async Task<TopUpDto> ToDtoAsync(Guid me, Payment p, string? url, CancellationToken ct)
    {
        var balance = (await store.WalletsOfAsync(me, ct)).FirstOrDefault(w => w.Type == nameof(WalletType.Customer))?.Available ?? 0;
        return new TopUpDto(p.Code, p.Amount, p.Status.ToString(), balance, url);
    }
}

public sealed class TopUpRequestValidator : AbstractValidator<TopUpRequest>
{
    public const decimal Min = 50, Max = 20_000;

    public TopUpRequestValidator()
    {
        RuleFor(x => x.Amount).InclusiveBetween(Min, Max).WithMessage($"Top up between {Min:0} and {Max:N0} EGP.")
            .Must(a => a == decimal.Truncate(a)).WithMessage("Whole pounds only.");
    }
}

/// <summary>The gateway confirmed a top-up: the money goes to the wallet.</summary>
public sealed class WalletTopUpPaymentListener(ILedger ledger, IUnitOfWork unitOfWork, INotifier notifier) : IPaymentListener
{
    public PaymentPurpose Purpose => PaymentPurpose.WalletTopUp;

    public Task PaidAsync(Payment payment, CancellationToken ct) => WalletTopUpService.CreditAsync(ledger, unitOfWork, notifier, payment, ct);

    public Task FailedAsync(Payment payment, CancellationToken ct) => Task.CompletedTask;
}
