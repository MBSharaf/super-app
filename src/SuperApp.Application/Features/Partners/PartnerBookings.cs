using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Application.Features.Partners;

/// <summary>
/// GET /partner/bookings — <c>Status</c>: upcoming (default), past, cancelled, noshow, all. <c>Q</c> searches the
/// booking code, the customer's or walk-in's name and phone. Dates are booking dates (yyyy-MM-dd).
/// </summary>
public sealed record PartnerBookingsQuery(long? PitchId = null, DateOnly? From = null, DateOnly? To = null, string? Status = null, string? Q = null, int Page = 1, int PageSize = 20);

/// <summary>A booking as the venue sees it. <c>Source</c>: App, WalkIn, Package, Series, Tournament, FreeSlot.</summary>
public sealed record PartnerBookingDto(
    string Code,
    long PitchId,
    string PitchName,
    DateOnly Date,
    int StartHour,
    int Duration,
    string CustomerName,
    string? CustomerPhone,
    string Source,
    decimal PitchAmount,
    decimal ServiceFee,
    decimal Total,
    decimal CommissionRate,
    bool PayAtVenue,
    string Status,
    DateTime? CheckedInAt,
    DateTime? CancelledAt,
    decimal RefundAmount,
    DateTime CreatedAt);

/// <summary>POST /partner/bookings/walk-in — a booking taken at the venue (no service fee, no commission).</summary>
public sealed record WalkInRequest(long PitchId, DateOnly Date, int Hour, int Duration, string Name, string? Phone);

/// <summary>POST /partner/bookings/{code}/cancel — the venue cancels; the customer gets everything back.</summary>
public sealed record VenueCancelRequest(string Reason);

/// <summary>The partner's bookings in the database (only bookings on the partner's own pitches).</summary>
public interface IPartnerBookingsRepository
{
    Task<PagedResult<PartnerBookingDto>> SearchAsync(long partnerId, PartnerBookingsQuery query, DateOnly today, CancellationToken ct);

    /// <summary>The same filter without paging, at most <paramref name="max"/> rows (for the Excel file).</summary>
    Task<IReadOnlyList<PartnerBookingDto>> AllAsync(long partnerId, PartnerBookingsQuery query, DateOnly today, int max, CancellationToken ct);

    Task<PartnerBookingDto?> GetAsync(long partnerId, string code, CancellationToken ct);

    /// <summary>A booking on one of the partner's pitches, tracked for changes.</summary>
    Task<Booking?> FindAsync(long partnerId, string code, CancellationToken ct);

    /// <summary>A pitch of the partner: slug and name.</summary>
    Task<(string Slug, string Name)?> PitchAsync(long partnerId, long pitchId, CancellationToken ct);
}
