using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Money;

public sealed record WalletDto(string Type, decimal Pending, decimal Available, decimal Settled, string Currency);

public sealed record LedgerRowDto(long Id, string Kind, string Bucket, decimal Amount, string RefType, long RefId, DateTime? AvailableAt, DateTime CreatedAt);

public sealed record ReceiptDto(string Number, string Kind, decimal Amount, string RefType, string? RefCode, DateTime CreatedAt);

/// <summary>The money tables (money.Wallets, LedgerEntries, Refunds, Receipts). Adds are saved by the caller.</summary>
public interface IMoneyStore
{
    /// <summary>The wallet of this owner and type, made when missing (platform wallets have no owner).</summary>
    Task<Wallet> WalletAsync(Guid? ownerUserId, WalletType type, CancellationToken ct);

    void Add(LedgerEntry entry);

    void Add(Refund refund);

    void Add(Receipt receipt);

    Task<string> NewReceiptNumberAsync(ReceiptKind kind, CancellationToken ct);

    Task<Guid> PitchOwnerAsync(long pitchId, CancellationToken ct);

    Task<Payment?> PaymentAsync(long paymentId, CancellationToken ct);

    Task<IReadOnlyList<WalletDto>> WalletsOfAsync(Guid userId, CancellationToken ct);

    Task<PagedResult<LedgerRowDto>> LedgerOfAsync(Guid userId, WalletType type, int page, int pageSize, CancellationToken ct);

    Task<IReadOnlyList<ReceiptDto>> ReceiptsOfAsync(Guid userId, CancellationToken ct);

    Task<ReceiptDto?> ReceiptAsync(Guid userId, string number, CancellationToken ct);
}

/// <summary>
/// The double-entry side of bookings. A paid booking: the partner's earning (total − service fee − commission) goes to its
/// bookings wallet as Pending until 24 hours after kick-off; commission and service fee go to the platform revenue wallet.
/// A refund goes to the customer's Super App wallet at once and takes back each share in proportion. Every payment and
/// refund gets a receipt (PDF on demand).
/// </summary>
public interface ILedger
{
    /// <summary>Adds the rows for a paid booking (saved by the caller; the booking must have its id).</summary>
    Task BookingPaidAsync(Booking booking, CancellationToken ct);

    /// <summary>Adds the refund of <c>booking.RefundAmount</c> (nothing when 0 or the booking was not paid in the app).</summary>
    Task BookingRefundAsync(Booking booking, Guid requestedById, string reason, CancellationToken ct);

    /// <summary>Something Super App sells itself (Fantasy Pass…): all of it to platform revenue, and a receipt.</summary>
    Task PlatformSaleAsync(Payment payment, string refType, long refId, CancellationToken ct);

    /// <summary>
    /// Money given to a customer outside a cancellation (a dispute): to their Super App wallet at once, charged to the venue
    /// (its available balance, else its held money) or to Super App, with a refund receipt.
    /// </summary>
    Task CreditAsync(Guid customerId, decimal amount, Guid? chargedPartnerId, string refType, long refId, CancellationToken ct);
}

public sealed class Ledger(IMoneyStore store, ILoyaltyStore loyalty, IClock clock) : ILedger
{
    public static readonly TimeSpan HoldAfterKickOff = TimeSpan.FromHours(24);

    public async Task BookingPaidAsync(Booking booking, CancellationToken ct)
    {
        var payment = booking.Payment ?? (booking.PaymentId is { } id ? await store.PaymentAsync(id, ct) : null);
        var fullyDiscounted = !booking.PayAtVenue && booking.Total == 0 && booking.DiscountAmount > 0;
        if (!(payment is { Status: PaymentStatus.Paid } || fullyDiscounted) || booking.CustomerId is null)
        {
            return;
        }
        var voucher = await loyalty.VoucherOnBookingAsync(booking.Id, ct);
        var (earning, commission, fee) = Shares(booking, voucher);
        var group = Guid.NewGuid();
        var partner = await store.WalletAsync(await store.PitchOwnerAsync(booking.PitchId, ct), WalletType.PartnerBookings, ct);
        var revenue = await store.WalletAsync(null, WalletType.PlatformRevenue, ct);
        Post(partner, earning, LedgerBucket.Pending, LedgerKind.BookingEarning, booking, Release(booking), group);
        Post(revenue, commission, LedgerBucket.Available, LedgerKind.Commission, booking, null, group);
        Post(revenue, fee, LedgerBucket.Available, LedgerKind.ServiceFee, booking, null, group);
        Post(revenue, -voucher, LedgerBucket.Available, LedgerKind.Voucher, booking, null, group);
        if (booking.Total > 0)
        {
            await ReceiptAsync(booking.CustomerId.Value, ReceiptKind.Payment, booking.Total, booking.Id, ct);
        }
    }

    public async Task BookingRefundAsync(Booking booking, Guid requestedById, string reason, CancellationToken ct)
    {
        var payment = booking.PaymentId is { } id ? await store.PaymentAsync(id, ct) : null;
        if (booking.RefundAmount <= 0 || payment is not { Status: PaymentStatus.Paid or PaymentStatus.PartlyRefunded } || booking.CustomerId is null)
        {
            return;
        }
        var share = booking.RefundAmount / booking.Total;
        var (earning, _, _) = Shares(booking, await loyalty.VoucherOnBookingAsync(booking.Id, ct));
        var partnerBack = Math.Round(earning * share, 2);
        var platformBack = booking.RefundAmount - partnerBack;
        var group = Guid.NewGuid();
        var customer = await store.WalletAsync(booking.CustomerId, WalletType.Customer, ct);
        var partner = await store.WalletAsync(await store.PitchOwnerAsync(booking.PitchId, ct), WalletType.PartnerBookings, ct);
        var revenue = await store.WalletAsync(null, WalletType.PlatformRevenue, ct);
        Post(customer, booking.RefundAmount, LedgerBucket.Available, LedgerKind.Refund, booking, null, group);
        Post(partner, -partnerBack, LedgerBucket.Pending, LedgerKind.Refund, booking, Release(booking), group);
        Post(revenue, -platformBack, LedgerBucket.Available, LedgerKind.Refund, booking, null, group);
        store.Add(new Refund
        {
            Payment = payment, Amount = booking.RefundAmount, Destination = RefundDestination.SuperAppWallet, Reason = reason,
            PaidBy = booking.Status == BookingStatus.CancelledByVenue ? RefundPayer.Partner : RefundPayer.SuperApp,
            Status = RefundStatus.Done, RequestedById = requestedById,
        });
        payment.Status = booking.RefundAmount >= payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartlyRefunded;
        await ReceiptAsync(booking.CustomerId.Value, ReceiptKind.Refund, booking.RefundAmount, booking.Id, ct);
    }

    public async Task PlatformSaleAsync(Payment payment, string refType, long refId, CancellationToken ct)
    {
        if (payment.Status != PaymentStatus.Paid || payment.PayerUserId is not { } payer)
        {
            return;
        }
        var revenue = await store.WalletAsync(null, WalletType.PlatformRevenue, ct);
        PostRaw(revenue, payment.Amount, LedgerBucket.Available, LedgerKind.ServiceFee, refType, refId, null, Guid.NewGuid());
        store.Add(new Receipt { Number = await store.NewReceiptNumberAsync(ReceiptKind.Payment, ct), UserId = payer, Kind = ReceiptKind.Payment, Amount = payment.Amount, RefType = refType, RefId = refId });
    }

    public async Task CreditAsync(Guid customerId, decimal amount, Guid? chargedPartnerId, string refType, long refId, CancellationToken ct)
    {
        if (amount <= 0)
        {
            return;
        }
        var group = Guid.NewGuid();
        PostRaw(await store.WalletAsync(customerId, WalletType.Customer, ct), amount, LedgerBucket.Available, LedgerKind.Refund, refType, refId, null, group);
        if (chargedPartnerId is { } partnerUser)
        {
            var partner = await store.WalletAsync(partnerUser, WalletType.PartnerBookings, ct);
            var bucket = partner.AvailableBalance >= amount ? LedgerBucket.Available : LedgerBucket.Pending;
            PostRaw(partner, -amount, bucket, LedgerKind.Deduction, refType, refId, bucket == LedgerBucket.Pending ? clock.UtcNow : null, group);
        }
        else
        {
            PostRaw(await store.WalletAsync(null, WalletType.PlatformRevenue, ct), -amount, LedgerBucket.Available, LedgerKind.Refund, refType, refId, null, group);
        }
        store.Add(new Receipt { Number = await store.NewReceiptNumberAsync(ReceiptKind.Refund, ct), UserId = customerId, Kind = ReceiptKind.Refund, Amount = amount, RefType = refType, RefId = refId });
    }

    /// <summary>
    /// Partner earning (pitch amount − the venue's own promo discount − commission), commission and service fee. A Super Points
    /// voucher is paid by Super App, so earning + commission + fee − voucher = the total paid.
    /// </summary>
    private static (decimal Earning, decimal Commission, decimal Fee) Shares(Booking b, decimal voucher)
    {
        var net = b.PitchAmount - (b.DiscountAmount - voucher);
        var commission = Math.Round(net * b.CommissionRate / 100, 2);
        return (net - commission, commission, b.ServiceFee);
    }

    private static DateTime Release(Booking b) =>
        CairoTime.ToUtc(b.Date.ToDateTime(new TimeOnly(b.StartHour, 0))).AddHours(b.Duration) + HoldAfterKickOff;

    private void Post(Wallet wallet, decimal amount, LedgerBucket bucket, LedgerKind kind, Booking b, DateTime? availableAt, Guid group) =>
        PostRaw(wallet, amount, bucket, kind, "Booking", b.Id, availableAt, group);

    private void PostRaw(Wallet wallet, decimal amount, LedgerBucket bucket, LedgerKind kind, string refType, long refId, DateTime? availableAt, Guid group)
    {
        if (amount == 0)
        {
            return;
        }
        store.Add(new LedgerEntry
        {
            Wallet = wallet, Amount = amount, Bucket = bucket, Kind = kind, RefType = refType, RefId = refId,
            AvailableAt = availableAt, GroupId = group, CreatedAt = clock.UtcNow,
        });
        if (bucket == LedgerBucket.Pending)
        {
            wallet.PendingBalance += amount;
        }
        else
        {
            wallet.AvailableBalance += amount;
        }
    }

    private async Task ReceiptAsync(Guid userId, ReceiptKind kind, decimal amount, long bookingId, CancellationToken ct) =>
        store.Add(new Receipt { Number = await store.NewReceiptNumberAsync(kind, ct), UserId = userId, Kind = kind, Amount = amount, RefType = "Booking", RefId = bookingId });
}

/// <summary>GET /wallet, /wallet/ledger, /receipts — each person sees only their own.</summary>
public interface IWalletService
{
    Task<IReadOnlyList<WalletDto>> WalletsAsync(CancellationToken ct);

    Task<PagedResult<LedgerRowDto>> LedgerAsync(string? type, int page, int pageSize, CancellationToken ct);

    Task<IReadOnlyList<ReceiptDto>> ReceiptsAsync(CancellationToken ct);

    Task<ReceiptDto> ReceiptAsync(string number, CancellationToken ct);

    Task<byte[]> ReceiptPdfAsync(string number, CancellationToken ct);
}

public sealed class WalletService(IMoneyStore store, IMoneyDocuments documents, Payments.IPayerDirectory payers, ICurrentUser currentUser) : IWalletService
{
    public Task<IReadOnlyList<WalletDto>> WalletsAsync(CancellationToken ct) => store.WalletsOfAsync(Me(), ct);

    public Task<PagedResult<LedgerRowDto>> LedgerAsync(string? type, int page, int pageSize, CancellationToken ct)
    {
        if (!Enum.TryParse<WalletType>(type ?? nameof(WalletType.Customer), true, out var t) || t >= WalletType.PlatformRevenue)
        {
            throw new Common.Exceptions.BadRequestException("Unknown wallet type.", "wallet_type");
        }
        return store.LedgerOfAsync(Me(), t, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
    }

    public Task<IReadOnlyList<ReceiptDto>> ReceiptsAsync(CancellationToken ct) => store.ReceiptsOfAsync(Me(), ct);

    public async Task<ReceiptDto> ReceiptAsync(string number, CancellationToken ct) =>
        await store.ReceiptAsync(Me(), number, ct) ?? throw new Common.Exceptions.NotFoundException("Receipt not found.");

    public async Task<byte[]> ReceiptPdfAsync(string number, CancellationToken ct) =>
        documents.Receipt(await ReceiptAsync(number, ct), (await payers.FindAsync(Me(), ct)).Name);

    private Guid Me() => currentUser.UserId ?? throw new Common.Exceptions.UnauthorizedException();
}

/// <summary>Money PDFs: payment and refund receipts (later payout advice and statements).</summary>
public interface IMoneyDocuments
{
    byte[] Receipt(ReceiptDto receipt, string customerName);
}
