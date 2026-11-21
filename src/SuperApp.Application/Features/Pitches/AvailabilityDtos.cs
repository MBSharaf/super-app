namespace SuperApp.Application.Features.Pitches;

/// <summary>GET /pitches/{slug}/availability — <c>From</c> defaults to today (Cairo), <c>Days</c> 1–14 (default 7).</summary>
public sealed record AvailabilityQuery(DateOnly? From = null, int Days = 7);

/// <summary>
/// One bookable hour. <c>Hour</c> is the hour on this day's grid (25 = 1 AM of the night after); <c>Date</c> and
/// <c>CalendarHour</c> are where the booking is stored. Status: free, booked, held, closed, past.
/// </summary>
public sealed record SlotDto(int Hour, DateOnly Date, int CalendarHour, string Status, decimal Price, decimal BasePrice, decimal OfferPercent, bool IsPeak, int Waiting);

public sealed record HolidayBadgeDto(string NameEn, string NameAr, bool IsEstimated);

/// <summary>
/// One half hour of a padel court. <c>Time</c> "19:30"; <c>Hour</c>/<c>Minute</c> on this day's grid (25 = 1 AM of the
/// night after); <c>Date</c> and <c>Index</c> (hour × 2, + 1 for :30) are where it is stored. <c>Price</c> is for the
/// half hour. A booking starts on a free half hour and takes 2, 3 or 4 free ones in a row.
/// </summary>
public sealed record HalfHourDto(string Time, int Hour, int Minute, DateOnly Date, int Index, string Status, decimal Price, bool IsPeak);

/// <summary><c>Hours</c> for a football pitch; <c>Halves</c> for a padel court (then <c>Hours</c> is empty).</summary>
public sealed record DayDto(DateOnly Date, int DayOfWeek, bool IsClosedDay, HolidayBadgeDto? Holiday, IReadOnlyList<SlotDto> Hours, IReadOnlyList<HalfHourDto>? Halves = null);

/// <summary><c>Sport</c> Football | Padel; <c>Durations</c> the minutes a padel court offers (60 / 90 / 120).</summary>
public sealed record AvailabilityDto(string Slug, IReadOnlyList<DayDto> Days, string Sport = "Football", IReadOnlyList<int>? Durations = null);

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
    IReadOnlyDictionary<(DateOnly Date, int Hour), int> Waiting,
    Domain.Enums.Sport Sport = Domain.Enums.Sport.Football,
    Domain.Enums.CourtDurations Durations = Domain.Enums.CourtDurations.None);

public sealed record ClosureData(DateOnly FromDate, DateOnly ToDate, int? FromHour, int? ToHour);

/// <summary>A quiet-hour offer: weekdays as a bit mask (bit 0 = Sunday), hours [FromHour, ToHour).</summary>
public sealed record OfferData(int DaysMask, int FromHour, int ToHour, decimal DiscountPercent, DateOnly StartsOn, DateOnly EndsOn);
