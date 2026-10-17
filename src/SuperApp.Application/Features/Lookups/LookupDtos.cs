namespace SuperApp.Application.Features.Lookups;

public sealed record GovernorateDto(int Id, string Code, string NameEn, string NameAr);

public sealed record AreaDto(int Id, int GovernorateId, string NameEn, string NameAr, decimal? Lat, decimal? Lng);

public sealed record NamedLookupDto(int Id, string Key, string NameEn, string NameAr, string? Icon);

public sealed record CancellationPolicyDto(int Id, string Key, string NameEn, string NameAr, int FullRefundHours, int HalfRefundHours, string SummaryEn, string SummaryAr);

public sealed record SellerLevelDto(
    int Id, string Key, string NameEn, string NameAr, int Rank, int SalesNeeded, decimal MinRating, int? ActiveDesigns, int? ActiveCodes,
    decimal BaseDiscount, decimal StoreCommission, string ReviewLane, int SearchBoost, decimal SampleDiscount, int FreeSamplesPerMonth,
    decimal FantasyDiscount, bool BulkUpload, bool TeamKits, bool EarlyAccess, string AnalyticsTier, string SupportTier);

public sealed record HolidayDto(DateOnly Date, string NameEn, string NameAr, bool IsEstimated);

/// <summary>Reads the public reference data (API §2.4). Implemented over the database in Infrastructure.</summary>
public interface ILookupsReader
{
    Task<IReadOnlyList<GovernorateDto>> GovernoratesAsync(CancellationToken ct);

    Task<IReadOnlyList<AreaDto>> AreasAsync(int? governorateId, CancellationToken ct);

    Task<IReadOnlyList<NamedLookupDto>> FacilitiesAsync(CancellationToken ct);

    Task<IReadOnlyList<CancellationPolicyDto>> CancellationPoliciesAsync(CancellationToken ct);

    Task<IReadOnlyList<NamedLookupDto>> ProductCategoriesAsync(CancellationToken ct);

    Task<IReadOnlyList<NamedLookupDto>> DesignCategoriesAsync(CancellationToken ct);

    Task<IReadOnlyList<SellerLevelDto>> SellerLevelsAsync(CancellationToken ct);

    /// <summary>Only the settings customers may see (fees, VAT, delivery, points value, pop-up timing…), by key.</summary>
    Task<IReadOnlyDictionary<string, object?>> PublicSettingsAsync(CancellationToken ct);

    Task<IReadOnlyList<HolidayDto>> HolidaysAsync(int year, CancellationToken ct);
}
