using SuperApp.Application.Features.Pitches;

namespace SuperApp.Application.Features.Partners;

/// <summary>A venue (place) of the signed-in partner.</summary>
public sealed record VenueDto(long Id, string Name, int AreaId, string AreaEn, string AreaAr, string Address, decimal Lat, decimal Lng, string? Phone, int PitchesCount);

/// <summary>POST / PUT /partner/venues</summary>
public sealed record VenueRequest(string Name, int AreaId, string Address, decimal Lat, decimal Lng, string? Phone);

/// <summary>One row of "My pitches". Status: WaitingForReview, Live, Rejected, ClosedByOwner, ClosedByAdmin.</summary>
public sealed record MyPitchDto(
    long Id,
    string Slug,
    string Name,
    long VenueId,
    string VenueName,
    int Format,
    decimal PricePerHour,
    decimal? PeakPricePerHour,
    string Status,
    string? RejectReason,
    decimal Rating,
    int ReviewsCount,
    PhotoDto? Photo,
    string PaymentMode = "Deposit",
    string Sport = "Football",
    bool IsPanoramic = false);

/// <summary>GET /partner/pitches/{id} — everything the edit form needs. Send <c>RowVersion</c> back with the edit.</summary>
public sealed record PitchEditDto(
    long Id,
    string Slug,
    long VenueId,
    string Name,
    int Format,
    string Surface,
    bool IsIndoor,
    decimal PricePerHour,
    decimal? PeakPricePerHour,
    int? PeakFromHour,
    string? DescriptionEn,
    string? DescriptionAr,
    string PolicyKey,
    bool AllowCash,
    IReadOnlyList<string> Facilities,
    IReadOnlyList<OpeningHourDto> OpeningHours,
    IReadOnlyList<PartnerPhotoDto> Photos,
    string Status,
    string? RejectReason,
    string RowVersion,
    string PaymentMode = "Deposit",
    string Sport = "Football",
    bool IsPanoramic = false,
    IReadOnlyList<int>? Durations = null,
    decimal? RacketPrice = null,
    decimal? BallsPrice = null);

/// <summary>
/// POST /partner/pitches (new pitch → waiting for review) and PUT /partner/pitches/{id} (edit; <c>RowVersion</c> from the
/// GET is required, a newer change → 409 <c>stale</c>). <c>OpeningHours</c>: one row per weekday (0 = Sunday … 6),
/// <c>CloseHour</c> up to 30 for after midnight (26 = 2 AM). <c>PaymentMode</c>: <c>Deposit</c> (a deposit in the app, the rest
/// at the pitch) or <c>FullOnline</c> (everything in the app; Super Malaab pays the venue). A change applies to new bookings only.
/// Padel courts (<c>Sport</c> = Padel, <c>Format</c> 2): <c>IsPanoramic</c> glass walls, <c>Durations</c> the lengths players can
/// book (60, 90, 120 minutes; at least one), and the rental prices of a racket and a tube of balls (null = not rented).
/// The sport is chosen when the pitch is added and cannot change later.
/// </summary>
public sealed record PitchRequest(
    long VenueId,
    string Name,
    int Format,
    string Surface,
    bool IsIndoor,
    decimal PricePerHour,
    decimal? PeakPricePerHour,
    int? PeakFromHour,
    string? DescriptionEn,
    string? DescriptionAr,
    string PolicyKey,
    bool AllowCash,
    IReadOnlyList<string> Facilities,
    IReadOnlyList<OpeningHourDto> OpeningHours,
    string? RowVersion = null,
    string PaymentMode = "Deposit",
    string Sport = "Football",
    bool IsPanoramic = false,
    IReadOnlyList<int>? Durations = null,
    decimal? RacketPrice = null,
    decimal? BallsPrice = null);

/// <summary>A pitch photo in the dashboard: <c>Id</c> is used to delete or reorder it.</summary>
public sealed record PartnerPhotoDto(long Id, string? Url, string? Key);

/// <summary>PUT /partner/pitches/{id}/photos/order — every photo id once, in the new order.</summary>
public sealed record PhotoOrderRequest(IReadOnlyList<long> PhotoIds);

/// <summary>DELETE /partner/pitches/{id} — what happened to the upcoming bookings.</summary>
public sealed record PitchRemovedDto(long Id, int CancelledBookings, decimal Refunded);

/// <summary>Blocked days or hours. Hours are calendar hours [FromHour, ToHour) on every date of the range; none = whole days.</summary>
public sealed record ClosureDto(long Id, DateOnly FromDate, DateOnly ToDate, int? FromHour, int? ToHour, string Reason, DateTime CreatedAt);

public sealed record ClosureRequest(DateOnly FromDate, DateOnly ToDate, int? FromHour, int? ToHour, string Reason);
