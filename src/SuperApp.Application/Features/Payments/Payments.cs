using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Payments;

/// <summary>What the gateway needs to open a payment page.</summary>
public sealed record GatewayStart(string MerchantRef, decimal Amount, string Description, string PayerName, string PayerEmail, string PayerPhone, string ReturnPath);

/// <summary>The gateway's answer: the page to send the payer to, and the gateway's order id.</summary>
public sealed record GatewaySession(string RedirectUrl, string ProviderRef);

/// <summary>A verified gateway callback. <c>MerchantRef</c> is the one sent in <see cref="GatewayStart"/>.</summary>
public sealed record GatewayEvent(string MerchantRef, string? ProviderRef, string? TransactionId, bool Success, bool Pending, decimal Amount, string RawJson);

/// <summary>
/// A payment company (INTEGRATIONS §3). The simulated gateway pays at once and is used while no keys are in .env;
/// Paymob is used when <c>PAYMENTS__PAYMOB__SECRETKEY</c> is set. The browser redirect never confirms a payment —
/// only a verified webhook does.
/// </summary>
public interface IPaymentGateway
{
    string Provider { get; }

    bool IsSimulated { get; }

    Task<GatewaySession> StartAsync(GatewayStart request, CancellationToken ct);

    /// <summary>Checks the signature; null when it is wrong or the callback is not a transaction.</summary>
    GatewayEvent? VerifyWebhook(IReadOnlyDictionary<string, string> query, string body);

    /// <summary>Gives money back through the gateway; returns the gateway's refund reference.</summary>
    Task<string> RefundAsync(string transactionId, decimal amount, CancellationToken ct);
}

public interface IPaymentRepository
{
    Task<string> NewCodeAsync(CancellationToken ct);

    void Add(Payment payment);

    Task<Payment?> FindAsync(string code, CancellationToken ct);

    Task<Payment?> FindByKeyAsync(string idempotencyKey, CancellationToken ct);
}

/// <summary>Called when a payment for its purpose is confirmed or fails (bookings, later orders, passes…).</summary>
public interface IPaymentListener
{
    PaymentPurpose Purpose { get; }

    Task PaidAsync(Payment payment, CancellationToken ct);

    Task FailedAsync(Payment payment, CancellationToken ct);
}

/// <summary>
/// What a service asks for: who pays, how much, for what. <c>FromWallet</c> pays from the payer's Super App wallet at once
/// (not enough money → 422 insufficient_funds); <c>IdempotencyKey</c> defaults to a new one.
/// </summary>
public sealed record PaymentRequest(PaymentPurpose Purpose, decimal Amount, Guid PayerId, string Description, string ReturnPath, bool FromWallet = false,
    string? IdempotencyKey = null);

/// <summary>The payment record and, for a real gateway, the page to send the payer to (null = already paid).</summary>
public sealed record PaymentStarted(Payment Payment, string? RedirectUrl);

public sealed record PaymentStatusDto(string Code, string Purpose, decimal Amount, string Status, string Provider, DateTime? PaidAt, string? RedirectUrl);

public interface IPaymentsService
{
    /// <summary>Adds the payment (not saved: the caller saves it with its booking / order).</summary>
    Task<PaymentStarted> StartAsync(PaymentRequest request, CancellationToken ct);

    Task<PaymentStatusDto> GetAsync(string code, CancellationToken ct);

    /// <summary>A new payment page for an unpaid payment. Paid → 422 already_paid.</summary>
    Task<PaymentStatusDto> RetryAsync(string code, CancellationToken ct);

    /// <summary>Gateway callback. Wrong signature → 400; a repeated callback changes nothing.</summary>
    Task WebhookAsync(string provider, IReadOnlyDictionary<string, string> query, string body, CancellationToken ct);
}

public sealed class PaymentsService(
    IPaymentGateway gateway,
    IPaymentRepository payments,
    IEnumerable<IPaymentListener> listeners,
    IPayerDirectory payers,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    Money.ILedger ledger) : IPaymentsService
{
    public const string WalletProvider = "wallet";

    public async Task<PaymentStarted> StartAsync(PaymentRequest request, CancellationToken ct)
    {
        var payment = new Payment
        {
            Code = await payments.NewCodeAsync(ct), PayerUserId = request.PayerId, Provider = request.FromWallet ? WalletProvider : gateway.Provider,
            Purpose = request.Purpose, Method = request.FromWallet ? PaymentMethod.SuperAppWallet : PaymentMethod.Card, Amount = request.Amount,
            IdempotencyKey = request.IdempotencyKey ?? Guid.NewGuid().ToString("N"), Status = PaymentStatus.Pending,
        };
        if (request.FromWallet)
        {
            await ledger.PayFromWalletAsync(payment, ct);
        }
        payments.Add(payment);
        if (request.FromWallet || gateway.IsSimulated)
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = clock.UtcNow;
            return new PaymentStarted(payment, null);
        }
        var url = await OpenSessionAsync(payment, request.Description, request.ReturnPath, ct);
        return new PaymentStarted(payment, url);
    }

    public async Task<PaymentStatusDto> GetAsync(string code, CancellationToken ct) => ToDto(await OwnAsync(code, ct), null);

    public async Task<PaymentStatusDto> RetryAsync(string code, CancellationToken ct)
    {
        var payment = await OwnAsync(code, ct);
        if (payment.Status == PaymentStatus.Paid)
        {
            throw new BusinessRuleException("This payment is already paid.", "already_paid");
        }
        if (payment.Status != PaymentStatus.Pending && payment.Status != PaymentStatus.Failed)
        {
            throw new BusinessRuleException("This payment can no longer be paid.", "payment_closed");
        }
        payment.Status = PaymentStatus.Pending;
        var url = await OpenSessionAsync(payment, payment.Purpose.ToString(), "#/bookings", ct);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(payment, url);
    }

    public async Task WebhookAsync(string provider, IReadOnlyDictionary<string, string> query, string body, CancellationToken ct)
    {
        if (!string.Equals(provider, gateway.Provider, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotFoundException("Unknown payment provider.");
        }
        var e = gateway.VerifyWebhook(query, body) ?? throw new BadRequestException("The callback signature is not valid.", "bad_signature");
        var payment = await payments.FindAsync(e.MerchantRef.Split('.')[0], ct);
        if (payment is null || e.Pending || payment.Status is PaymentStatus.Paid or PaymentStatus.Refunded or PaymentStatus.PartlyRefunded)
        {
            return;
        }
        var paid = e.Success && e.Amount == payment.Amount;
        payment.RawWebhookJson = e.RawJson;
        payment.ProviderRef ??= e.ProviderRef;
        payment.Status = paid ? PaymentStatus.Paid : PaymentStatus.Failed;
        payment.PaidAt = paid ? clock.UtcNow : null;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Site, paid ? "payment.paid" : "payment.failed",
            $"Payment {payment.Code} {(paid ? "paid" : "failed")} ({payment.Amount:0.##} EGP)", "Payment", payment.Code, ct: ct);
        foreach (var l in listeners.Where(l => l.Purpose == payment.Purpose))
        {
            await (paid ? l.PaidAsync(payment, ct) : l.FailedAsync(payment, ct));
        }
    }

    /// <summary>Each session gets its own merchant ref (code.timestamp) so a retry is a new order at the gateway.</summary>
    private async Task<string> OpenSessionAsync(Payment payment, string description, string returnPath, CancellationToken ct)
    {
        var payer = await payers.FindAsync(payment.PayerUserId!.Value, ct);
        var reference = $"{payment.Code}.{new DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds()}";
        var session = await gateway.StartAsync(new GatewayStart(reference, payment.Amount, description, payer.Name, payer.Email, payer.Phone, returnPath), ct);
        payment.ProviderRef = session.ProviderRef;
        return session.RedirectUrl;
    }

    private async Task<Payment> OwnAsync(string code, CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        var payment = await payments.FindAsync(code, ct);
        return payment is not null && payment.PayerUserId == me ? payment : throw new NotFoundException("Payment not found.");
    }

    private static PaymentStatusDto ToDto(Payment p, string? url) =>
        new(p.Code, p.Purpose.ToString(), p.Amount, p.Status.ToString(), p.Provider, p.PaidAt, url);
}

/// <summary>The payer's name, email and phone for the gateway's billing data.</summary>
public interface IPayerDirectory
{
    Task<(string Name, string Email, string Phone)> FindAsync(Guid userId, CancellationToken ct);
}
