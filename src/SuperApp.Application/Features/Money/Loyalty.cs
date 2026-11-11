using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Money;

public sealed record PointsDto(int Balance, int MinRedeem, decimal PointValue, int EarnPer, IReadOnlyList<PointRowDto> History);

/// <summary>One line of the points history. <c>Id</c> lets the site show the "you earned" pop-up once per entry.</summary>
public sealed record PointRowDto(int Points, string Kind, string? Reason, DateTime CreatedAt, long Id = 0);

public sealed record VoucherDto(string Code, decimal Amount, string Source, string Status, DateTime? ExpiresAt, DateTime CreatedAt);

public sealed record RedeemRequest(decimal Amount);

/// <summary>POST /codes/check — scope Bookings; pitchSlug and total (pitch amount) to check the rules.</summary>
public sealed record CodeCheckRequest(string Code, string? PitchSlug, decimal Total);

public sealed record CodeCheckDto(bool Valid, string Code, string Kind, decimal Discount, string? Reason);

/// <summary>Promo code or voucher applied to a booking: what it takes off and who funds it.</summary>
public sealed record BookingDiscount(PromoCode? Promo, decimal PromoDiscount, Voucher? Voucher, decimal VoucherAmount)
{
    public decimal Total => PromoDiscount + VoucherAmount;
}

public interface ILoyaltyStore
{
    Task<int> BalanceAsync(Guid userId, CancellationToken ct);

    Task<IReadOnlyList<PointRowDto>> HistoryAsync(Guid userId, CancellationToken ct);

    Task<bool> HasPointsForAsync(Guid userId, PointKind kind, string refType, long refId, CancellationToken ct);

    void Add(PointEntry entry);

    void Add(Voucher voucher);

    void Add(PromoCodeUse use);

    Task<string> NewVoucherCodeAsync(CancellationToken ct);

    Task<IReadOnlyList<VoucherDto>> VouchersAsync(Guid userId, CancellationToken ct);

    Task<Voucher?> VoucherAsync(string code, CancellationToken ct);

    Task<PromoCode?> PromoAsync(string code, CancellationToken ct);

    Task<int> UsesByAsync(long promoId, Guid userId, CancellationToken ct);

    Task<decimal> VoucherOnBookingAsync(long bookingId, CancellationToken ct);
}

/// <summary>
/// Super Points (settings loyalty.*): earned by <see cref="SuperPoints"/> (1 point per EGP paid) and +20 for a review; from <c>minRedeem</c> points they turn into a voucher (multiples of 100 EGP, 20 points = 1 EGP
/// at pointValue 0.05) that pays part of a booking. Partner promo codes and vouchers are checked here.
/// </summary>
public sealed class LoyaltyService(
    ILoyaltyStore store,
    IBookingRepository bookings,
    ISettingsReader settings,
    INotifier notifier,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IClock clock)
{
    public const int ReviewBonus = 20;

    /// <summary>+20 for reviewing a game, once per booking. Not saved: the caller saves.</summary>
    public async Task EarnForReviewAsync(Guid userId, long bookingId, string bookingCode, CancellationToken ct)
    {
        if (!await store.HasPointsForAsync(userId, PointKind.Review, "Booking", bookingId, ct))
        {
            store.Add(new PointEntry { UserId = userId, Points = ReviewBonus, Kind = PointKind.Review, RefType = "Booking", RefId = bookingId, Reason = $"Review {bookingCode}", CreatedAt = clock.UtcNow });
        }
    }

    public async Task<PointsDto> PointsAsync(CancellationToken ct)
    {
        var me = Me();
        return new PointsDto(await store.BalanceAsync(me, ct), (int)await settings.DecimalAsync("loyalty.minRedeem", 1000m, ct),
            await settings.DecimalAsync("loyalty.pointValue", 0.05m, ct), (int)await settings.DecimalAsync("loyalty.earnPer", 1m, ct), await store.HistoryAsync(me, ct));
    }

    public Task<IReadOnlyList<VoucherDto>> VouchersAsync(CancellationToken ct) => store.VouchersAsync(Me(), ct);

    /// <summary>
    /// Points → a voucher of <c>amount</c> EGP (multiples of 5 EGP = 100 points; at least loyalty.minRedeem points, 1,000 since
    /// 2026-11-11). 422 not_multiple / under_minimum / not_enough_points.
    /// </summary>
    public async Task<VoucherDto> RedeemAsync(RedeemRequest request, CancellationToken ct)
    {
        var me = Me();
        Rule(request.Amount >= 5 && request.Amount % 5 == 0 && request.Amount <= 10_000, "Choose an amount in steps of 5 EGP.", "not_multiple");
        var value = await settings.DecimalAsync("loyalty.pointValue", 0.05m, ct);
        var needed = (int)Math.Ceiling(request.Amount / value);
        var balance = await store.BalanceAsync(me, ct);
        var minimum = await settings.DecimalAsync("loyalty.minRedeem", 1000m, ct);
        Rule(balance >= minimum && needed >= minimum, $"The smallest voucher is {minimum:N0} points.", "under_minimum");
        Rule(balance >= needed, $"That needs {needed:N0} points; you have {balance:N0}.", "not_enough_points");
        var voucher = new Voucher { Code = await store.NewVoucherCodeAsync(ct), UserId = me, Amount = request.Amount, Source = VoucherSource.Points,
            PointsUsed = needed, Status = VoucherStatus.Active, ExpiresAt = clock.UtcNow.AddYears(1) };
        store.Add(voucher);
        store.Add(new PointEntry { UserId = me, Points = -needed, Kind = PointKind.VoucherRedemption, RefType = "Voucher", Reason = $"Voucher {voucher.Code}", CreatedAt = clock.UtcNow });
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(me, new NotificationMessage(NotificationCategory.Money, $"Voucher {voucher.Code}: {voucher.Amount:0} EGP",
            "Use it when you book a pitch.", "#/wallet", NotificationTone.Success), ct);
        return ToDto(voucher);
    }

    /// <summary>Checks a promo code or one of my vouchers against a pitch and amount (never throws for a bad code).</summary>
    public async Task<CodeCheckDto> CheckAsync(CodeCheckRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var pitch = request.PitchSlug is null ? null : await bookings.PitchAsync(request.PitchSlug, ct);
        try
        {
            var d = await ForBookingAsync(Me(), code.StartsWith("SP-", StringComparison.Ordinal) ? null : code, code.StartsWith("SP-", StringComparison.Ordinal) ? code : null,
                pitch?.Id ?? 0, pitch?.OwnerUserId, request.Total, request.Total, ct);
            return new CodeCheckDto(true, code, d.Voucher is null ? "promo" : "voucher", d.Total, null);
        }
        catch (BusinessRuleException ex)
        {
            return new CodeCheckDto(false, code, "unknown", 0, ex.Message);
        }
    }

    /// <summary>
    /// The discount for a booking: a promo code (its rules, on the pitch amount) and / or a voucher of mine (all of it, up to
    /// what is left to pay). 422 with the reason when a code cannot be used.
    /// </summary>
    public async Task<BookingDiscount> ForBookingAsync(Guid user, string? promoCode, string? voucherCode, long pitchId, Guid? pitchOwner,
        decimal pitchAmount, decimal total, CancellationToken ct)
    {
        PromoCode? promo = null;
        var promoDiscount = 0m;
        if (!string.IsNullOrWhiteSpace(promoCode))
        {
            promo = await store.PromoAsync(promoCode.Trim().ToUpperInvariant(), ct);
            await CheckPromoAsync(promo, user, pitchId, pitchOwner, pitchAmount, ct);
            promoDiscount = promo!.DiscountType == DiscountType.Percent ? Math.Round(pitchAmount * promo.Value / 100, 2) : Math.Min(promo.Value, pitchAmount);
        }
        Voucher? voucher = null;
        var voucherAmount = 0m;
        if (!string.IsNullOrWhiteSpace(voucherCode))
        {
            voucher = await store.VoucherAsync(voucherCode.Trim().ToUpperInvariant(), ct);
            Rule(voucher is not null && voucher.UserId == user, "This voucher is not yours.", "voucher_unknown");
            Rule(voucher!.Status == VoucherStatus.Active && (voucher.ExpiresAt is null || voucher.ExpiresAt > clock.UtcNow), "This voucher was used or has expired.", "voucher_used");
            voucherAmount = Math.Min(voucher.Amount, total - promoDiscount);
        }
        return new BookingDiscount(promo, promoDiscount, voucher, voucherAmount);
    }

    private async Task CheckPromoAsync(PromoCode? promo, Guid user, long pitchId, Guid? pitchOwner, decimal pitchAmount, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(CairoTime.Now(clock));
        Rule(promo is not null, "This code does not exist.", "code_unknown");
        Rule(promo!.Status == CodeStatus.Active && promo.StartsOn <= today && (promo.EndsOn is null || promo.EndsOn >= today), "This code is not active.", "code_inactive");
        Rule(promo.Scope != CodeScope.Shop && promo.DiscountType != DiscountType.FreeDelivery, "This code is for the shop.", "code_scope");
        Rule(promo.Funding != CodeFunding.OwnerFunded || promo.OwnerUserId == pitchOwner, "This code is for another venue.", "code_other_venue");
        Rule(promo.Pitches.Count == 0 || promo.Pitches.Any(p => p.PitchId == pitchId), "This code is not for this pitch.", "code_other_pitch");
        Rule(pitchAmount >= promo.MinSpend, $"This code needs at least {promo.MinSpend:0} EGP.", "code_min_spend");
        Rule(promo.MaxUses is null || promo.UsesCount < promo.MaxUses, "This code has been used up.", "code_used_up");
        Rule(promo.PerCustomer is null || await store.UsesByAsync(promo.Id, user, ct) < promo.PerCustomer, "You already used this code.", "code_used_by_you");
    }

    /// <summary>Marks the code and voucher used by this booking (not saved: the caller saves).</summary>
    public void Use(BookingDiscount d, Booking booking)
    {
        if (d.Promo is { } promo && d.PromoDiscount > 0)
        {
            promo.UsesCount++;
            store.Add(new PromoCodeUse { PromoCodeId = promo.Id, UserId = booking.CustomerId!.Value, Booking = booking, DiscountAmount = d.PromoDiscount, RevenueAmount = booking.Total });
            booking.PromoCodeId = promo.Id;
        }
        if (d.Voucher is { } voucher && d.VoucherAmount > 0)
        {
            voucher.Status = VoucherStatus.Used;
            voucher.UsedOnType = "Booking";
        }
    }

    private static void Rule(bool ok, string message, string code)
    {
        if (!ok)
        {
            throw new BusinessRuleException(message, code);
        }
    }

    private static VoucherDto ToDto(Voucher v) => new(v.Code, v.Amount, v.Source.ToString(), v.Status.ToString(), v.ExpiresAt, v.CreatedAt);

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

/// <summary>
/// Owner's rule (2026-11-01): every EGP a player pays earns Super Points (1 point per <c>loyalty.earnPer</c> EGP, 1 by default),
/// at the moment it is paid — any payment in the app except a wallet top-up (the points come when the wallet money is spent),
/// and the rest of a booking paid at the pitch (at check-in). A refund takes the points of the refunded money back (never below 0).
/// Nothing here saves: the caller saves with its payment, booking or refund.
/// </summary>
public sealed class SuperPoints(ILoyaltyStore store, ISettingsReader settings, IClock clock)
{
    public const string PaymentRef = "Payment";

    public async Task<int> ForAmountAsync(decimal amount, CancellationToken ct)
    {
        var per = await settings.DecimalAsync("loyalty.earnPer", 1m, ct);
        return amount <= 0 || per <= 0 ? 0 : (int)Math.Floor(amount / per);
    }

    /// <summary>Points for a paid payment (called once, when it turns Paid).</summary>
    public async Task EarnForPaymentAsync(Payment payment, CancellationToken ct)
    {
        if (payment.Purpose == PaymentPurpose.WalletTopUp || payment.PayerUserId is not { } payer || payment.Status != PaymentStatus.Paid)
        {
            return;
        }
        var points = await ForAmountAsync(payment.Amount, ct);
        if (points > 0)
        {
            store.Add(new PointEntry { UserId = payer, Points = points, Kind = PointKind.Payment, RefType = PaymentRef, RefId = payment.Id == 0 ? null : payment.Id,
                Reason = $"{Label(payment.Purpose)} · {payment.Code}", CreatedAt = clock.UtcNow });
        }
    }

    /// <summary>Points for money paid to the venue at the pitch (once per booking).</summary>
    public async Task EarnAtPitchAsync(Booking booking, decimal amount, CancellationToken ct)
    {
        if (booking.CustomerId is not { } user || amount <= 0 || await store.HasPointsForAsync(user, PointKind.Booking, "Booking", booking.Id, ct))
        {
            return;
        }
        var points = await ForAmountAsync(amount, ct);
        if (points > 0)
        {
            store.Add(new PointEntry { UserId = user, Points = points, Kind = PointKind.Booking, RefType = "Booking", RefId = booking.Id,
                Reason = $"Paid at the pitch · {booking.Code}", CreatedAt = clock.UtcNow });
        }
    }

    /// <summary>A refund of <c>amount</c>: its points come back off the balance (as much as the balance allows).</summary>
    public async Task TakeBackAsync(Guid userId, decimal amount, string refType, long refId, CancellationToken ct)
    {
        var points = Math.Min(await ForAmountAsync(amount, ct), await store.BalanceAsync(userId, ct));
        if (points > 0)
        {
            store.Add(new PointEntry { UserId = userId, Points = -points, Kind = PointKind.Refund, RefType = refType, RefId = refId,
                Reason = $"Refund · {amount:0.##} EGP", CreatedAt = clock.UtcNow });
        }
    }

    private static string Label(PaymentPurpose purpose) => purpose switch
    {
        PaymentPurpose.Booking => "Booking deposit",
        PaymentPurpose.Package => "Package deposit",
        PaymentPurpose.GameShare => "Game",
        PaymentPurpose.TournamentFee => "Tournament entry",
        PaymentPurpose.ShopOrder => "Shop order",
        PaymentPurpose.FantasyPass => "Fantasy Pass",
        _ => "Payment",
    };
}
