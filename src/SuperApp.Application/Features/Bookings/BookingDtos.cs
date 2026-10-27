namespace SuperApp.Application.Features.Bookings;

/// <summary>
/// The hours asked for: <c>Date</c> is the day of the grid and <c>Hour</c> the grid hour (25 = 1 AM of the night
/// after), as GET /pitches/{slug}/availability shows them. <c>Duration</c> 1–4 hours in a row.
/// </summary>
public sealed record HoldRequest(string PitchSlug, DateOnly Date, int Hour, int Duration = 1);

/// <summary>POST /bookings — confirm. <c>AcceptWaiver</c> must be true (injury waiver).</summary>
/// <remarks><c>PromoCode</c>: a venue or Super App code; <c>VoucherCode</c>: one of my Super Points vouchers (SP-…).</remarks>
public sealed record CreateBookingRequest(string PitchSlug, DateOnly Date, int Hour, int Duration, bool PayAtVenue, bool AcceptWaiver, string? PromoCode = null, string? VoucherCode = null,
    bool PayFromWallet = false);

/// <summary>Price of each hour and the totals.</summary>
public sealed record PriceLine(int Hour, DateOnly Date, int CalendarHour, decimal Price);

public sealed record HoldResponse(DateTime ExpiresAt, IReadOnlyList<PriceLine> Hours, decimal PitchAmount, decimal ServiceFee, decimal Total);

/// <summary>A booking as its customer sees it. <c>QrToken</c> only on the booking's own page.</summary>
public sealed record BookingResponse(
    string Code,
    string PitchSlug,
    string PitchName,
    string Address,
    DateOnly Date,
    int StartHour,
    int Duration,
    decimal PitchAmount,
    decimal ServiceFee,
    decimal Total,
    bool PayAtVenue,
    string Status,
    string PolicyKey,
    decimal RefundAmount,
    DateTime CreatedAt,
    DateTime? CancelledAt,
    string? QrToken,
    string? PackageCode = null,
    string? PaymentUrl = null);

/// <summary>What a cancellation now refunds (the pitch's policy and the time left before kick-off).</summary>
public sealed record RefundPreview(string Code, double HoursLeft, int RefundPercent, decimal RefundAmount, bool CanCancel);

/// <summary>GET /bookings/mine — tab: upcoming (default), past, cancelled.</summary>
public sealed record MyBookingsQuery(string? Tab = null, int Page = 1, int PageSize = 20);

/// <summary>What the booking rules need about a pitch.</summary>
public sealed record BookablePitch(long Id, string Slug, string Name, string Address, Guid OwnerUserId, bool AllowCash, string PolicyKey,
    int FullRefundHours, int HalfRefundHours, decimal? PartnerCommission);
