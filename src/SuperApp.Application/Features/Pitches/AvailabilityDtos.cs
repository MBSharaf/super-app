namespace SuperApp.Application.Features.Pitches;

/// <summary>GET /pitches/{slug}/availability — <c>From</c> defaults to today (Cairo), <c>Days</c> 1–14 (default 7).</summary>
public sealed record AvailabilityQuery(DateOnly? From = null, int Days = 7);

/// <summary>
/// One bookable hour. <c>Hour</c> is the hour on this day's grid (25 = 1 AM of the night after); <c>Date</c> and
/// <c>CalendarHour</c> are where the booking is stored. Status: free, booked, held, closed, past.
/// </summary>
public sealed record SlotDto(int Hour, DateOnly Date, int CalendarHour, string Status, decimal Price, decimal BasePrice, decimal OfferPercent, bool IsPeak, int Waiting);

public sealed record HolidayBadgeDto(string NameEn, string NameAr, bool IsEstimated);

public sealed record DayDto(DateOnly Date, int DayOfWeek, bool IsClosedDay, HolidayBadgeDto? Holiday, IReadOnlyList<SlotDto> Hours);

public sealed record AvailabilityDto(string Slug, IReadOnlyList<DayDto> Days);

/// <summary>Everything the grid needs about one pitch for a date range (read in Infrastructure).</summary>
public sealed record AvailabilityData(
    long PitchId,
    decimal Price,
    decimal? PeakPrice,
    int? PeakFromHour,
    IReadOnlyList<OpeningHourDto> OpeningHours,
    IReadOnlySet<(DateOnly Date, int Hour)> Booked,
    IReadOnlySet<(DateOnly Date, int Hour)> Held,
    IReadOnlyList<ClosureData> Closures,
    IReadOnlyList<OfferData> Offers,
    IReadOnlyDictionary<DateOnly, HolidayBadgeDto> Holidays,
    IReadOnlyDictionary<(DateOnly Date, int Hour), int> Waiting);

public sealed record ClosureData(DateOnly FromDate, DateOnly ToDate, int? FromHour, int? ToHour);

/// <summary>A quiet-hour offer: weekdays as a bit mask (bit 0 = Sunday), hours [FromHour, ToHour).</summary>
public sealed record OfferData(int DaysMask, int FromHour, int ToHour, decimal DiscountPercent, DateOnly StartsOn, DateOnly EndsOn);
