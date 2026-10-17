namespace SuperApp.Application.Features.Pitches;

/// <summary>
/// GET /pitches filters (API §3.1). <c>Facilities</c> = comma-separated keys, all must match.
/// <c>Sort</c>: rating (default), price, -price, distance (needs lat and lng), name.
/// </summary>
public sealed record PitchSearchQuery(
    string? Q = null,
    int? AreaId = null,
    int? GovernorateId = null,
    int? Format = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Facilities = null,
    decimal? Lat = null,
    decimal? Lng = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>A photo: <c>Url</c> for uploaded files; demo photos only carry their <c>Key</c> (the site builds the address).</summary>
public sealed record PhotoDto(string? Url, string? Key);

/// <summary>One pitch in the list (card).</summary>
public sealed record PitchCardDto(
    string Slug,
    string Name,
    string VenueName,
    string AreaEn,
    string AreaAr,
    string GovernorateEn,
    decimal Lat,
    decimal Lng,
    int Format,
    string Surface,
    bool IsIndoor,
    decimal PricePerHour,
    decimal? PeakPricePerHour,
    decimal Rating,
    int ReviewsCount,
    PhotoDto? Photo,
    IReadOnlyList<string> Facilities,
    double? DistanceKm);

public sealed record OpeningHourDto(int DayOfWeek, int OpenHour, int CloseHour, bool IsClosed);

public sealed record PolicyDto(string Key, string NameEn, string NameAr, int FullRefundHours, int HalfRefundHours, string SummaryEn, string SummaryAr);

/// <summary>A package size the admin approved (matches per package and the discount).</summary>
public sealed record PackageOptionDto(long Id, int MatchCount, decimal DiscountPercent);

/// <summary>GET /pitches/{slug} — the pitch page.</summary>
public sealed record PitchDetailsDto(
    PitchCardDto Card,
    string Address,
    string? DescriptionEn,
    string? DescriptionAr,
    int? PeakFromHour,
    bool AllowCash,
    IReadOnlyList<PhotoDto> Photos,
    PolicyDto CancellationPolicy,
    IReadOnlyList<OpeningHourDto> OpeningHours,
    IReadOnlyList<PackageOptionDto> PackageOptions,
    string PartnerName);
