using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>The printable catalogue and a seller's designs.</summary>
public interface IDesignRepository
{
    /// <summary>Products on sale, priced for a level discount.</summary>
    Task<IReadOnlyList<SellerCatalogDto>> CatalogueAsync(decimal levelDiscount, CancellationToken ct);

    /// <summary>A printable product with colours and print areas (not tracked); null when retired or missing.</summary>
    Task<CatalogProduct?> CatalogProductAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<SellerDesignDto>> DesignsAsync(long sellerId, string? status, decimal levelDiscount, CancellationToken ct);

    Task<SellerDesignDto?> DesignDtoAsync(long sellerId, long id, decimal levelDiscount, CancellationToken ct);

    /// <summary>The seller's design with colours and tags, tracked for changes.</summary>
    Task<Design?> DesignAsync(long sellerId, long id, CancellationToken ct);

    void AddDesign(Design design);

    Task<int?> DesignCategoryIdAsync(string key, CancellationToken ct);

    /// <summary>Live, in review and paused designs (the level's "active designs").</summary>
    Task<int> ActiveDesignsAsync(long sellerId, CancellationToken ct);

    Task ClearFeaturedAsync(long sellerId, long designId, CancellationToken ct);
}

/// <summary>What a seller sold and earned, samples, analytics, calendar and store codes.</summary>
public interface ISellerInsightsRepository
{
    Task<PagedResult<SellerOrderDto>> OrdersAsync(long storeId, SellerOrdersQuery query, CancellationToken ct);

    /// <summary>Earnings of the store's items: pending, available after the hold days, last 30 days; paid samples come off the available part.</summary>
    Task<SellerKpisDto> KpisAsync(long sellerId, long storeId, int holdDays, CancellationToken ct);

    Task<IReadOnlyList<SampleDto>> SamplesAsync(long sellerId, CancellationToken ct);

    Task<int> FreeSamplesSinceAsync(long sellerId, DateTime since, CancellationToken ct);

    void AddSample(Sample sample);

    Task<(IReadOnlyList<AnalyticsDayDto> Days, IReadOnlyList<AnalyticsDesignDto> Designs)> AnalyticsAsync(long sellerId, long storeId, DateTime from, DateTime to, CancellationToken ct);

    Task<IReadOnlyList<CalendarEventDto>> HolidaysAsync(DateOnly from, DateOnly to, CancellationToken ct);

    Task<IReadOnlyList<SellerCodeDto>> CodesAsync(long storeId, CancellationToken ct);

    Task<PromoCode?> CodeAsync(long storeId, long id, CancellationToken ct);

    Task<int> ActiveCodesAsync(long storeId, long? exceptId, CancellationToken ct);

    Task<bool> CodeTakenAsync(string code, long? exceptId, CancellationToken ct);

    void AddCode(PromoCode code);

    Task<KycStatusDto> KycAsync(Guid userId, CancellationToken ct);
}

/// <summary>Design review, design reports and seller strikes for admins.</summary>
public interface IPodAdminRepository
{
    Task<PagedResult<ReviewDesignDto>> QueueAsync(int page, int pageSize, CancellationToken ct);

    /// <summary>A design with its seller and store, tracked for changes.</summary>
    Task<Design?> DesignAsync(long id, CancellationToken ct);

    Task<PagedResult<DesignReportDto>> ReportsAsync(int page, int pageSize, CancellationToken ct);

    Task<DesignReport?> ReportAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<DesignReport>> OpenReportsAsync(long designId, CancellationToken ct);

    void AddStrike(SellerStrike strike);

    Task<int> ActiveStrikesAsync(long sellerId, DateTime now, CancellationToken ct);
}
