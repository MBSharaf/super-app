using SuperApp.Application.Features.Pitches;
using SuperApp.Application.Features.Shop.Catalog;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>A printable product as a seller sees it: everything but the factory cost, with the base price for the seller's level.</summary>
public sealed record SellerCatalogDto(
    int Id,
    string Key,
    string NameEn,
    string NameAr,
    string Kind,
    string DescriptionEn,
    string DescriptionAr,
    decimal BasePrice,
    decimal BaseForMyLevel,
    decimal BackPrintExtra,
    decimal BigSizeExtra,
    decimal MaxPrice,
    bool HasBackPrint,
    string? Model3dUrl,
    string Status,
    IReadOnlyList<CatalogColorDto> Colors,
    IReadOnlyList<CatalogSizeDto> Sizes,
    IReadOnlyList<CatalogDetailDto> Details,
    IReadOnlyList<PrintAreaDto> PrintAreas);

/// <summary>
/// A design in the studio. <c>Base</c> = what Super App keeps per piece for this design (back print included when there is
/// back art, after the level discount); <c>Profit</c> = price − base. Send <c>RowVersion</c> back when saving (409 stale).
/// </summary>
public sealed record SellerDesignDto(
    long Id,
    int CatalogProductId,
    string CatalogKey,
    string ProductName,
    string? CategoryKey,
    string Title,
    string? Description,
    decimal Price,
    decimal Base,
    decimal MaxPrice,
    decimal Profit,
    IReadOnlyList<long> ColorIds,
    IReadOnlyList<string> Colors,
    IReadOnlyList<string> Tags,
    PhotoDto? FrontArt,
    PhotoDto? BackArt,
    PlacementDto? Front,
    PlacementDto? Back,
    string Status,
    string? RejectReason,
    bool SpotCheck,
    int Views,
    int Sales,
    DateTime? SubmittedAt,
    DateTime? ReviewedAt,
    DateTime CreatedAt,
    string RowVersion);

public sealed record NewDesignRequest(int CatalogProductId);

/// <summary>PUT /seller/designs/{id}: the draft's product, listing, colours, placement and price.</summary>
public sealed record DesignRequest(
    int CatalogProductId,
    string? CategoryKey,
    string Title,
    string? Description,
    decimal Price,
    IReadOnlyList<long> ColorIds,
    IReadOnlyList<string> Tags,
    PlacementDto? Front,
    PlacementDto? Back,
    string RowVersion);

/// <summary>After an art upload: the design, the pixel size and the print quality at full print width (vector, good ≥ 200 dpi, ok ≥ 150, low).</summary>
public sealed record ArtResultDto(SellerDesignDto Design, int Width, int Height, int? Dpi, string Quality);

/// <summary>Bulk upload settings shared by every file (Pro and Elite).</summary>
public sealed record BulkDesignRequest(int CatalogProductId, string CategoryKey, decimal Price, IReadOnlyList<long> ColorIds, IReadOnlyList<string>? Tags);

public sealed record LevelCardDto(string Key, string NameEn, string NameAr, int Rank, string? NextKey, string? NextNameEn, int SalesNeeded, decimal RatingNeeded,
    int LifetimeSales, decimal Rating, int? ActiveDesignsLimit, int ActiveDesigns, DateTime? FrozenUntil);

/// <summary>Earnings until the money phase: pending (not delivered, or delivered within the hold days) and available.</summary>
public sealed record SellerKpisDto(int LiveDesigns, int InReview, int ItemsSold30Days, decimal Earnings30Days, decimal PendingEarnings, decimal AvailableEarnings, int Followers);

/// <summary>One item the seller sold (printed or own product). FactoryStage is set for printed items.</summary>
public sealed record SellerOrderDto(string OrderCode, DateTime PlacedAt, long ItemId, string Kind, string Title, string Size, string Color, int Qty, decimal UnitPrice,
    decimal Earning, string Status, string? FactoryStage, string City, PhotoDto? Image);

public sealed record SellerOverviewDto(LevelCardDto Level, SellerKpisDto Kpis, IReadOnlyList<SellerDesignDto> BestDesigns, IReadOnlyList<SellerOrderDto> LatestOrders);

public sealed record SellerOrdersQuery(string? Status = null, int Page = 1, int PageSize = 20);

public sealed record SampleDto(long Id, long DesignId, string DesignTitle, string Color, string Size, string Address, decimal Price, bool IsFree, string Status, string? OrderCode, DateTime CreatedAt);

public sealed record SampleRequest(long DesignId, string Color, string Size, string Address);

public sealed record AnalyticsDayDto(DateOnly Date, int Items, decimal Earnings);

public sealed record AnalyticsDesignDto(long Id, string Title, int Views, int Items, decimal Earnings);

/// <summary>Analytics depth by level: Basic = totals and designs; Advanced adds days; export (Excel) from Elite.</summary>
public sealed record SellerAnalyticsDto(DateOnly From, DateOnly To, string Tier, int Items, decimal Earnings, int Views, IReadOnlyList<AnalyticsDayDto> Days, IReadOnlyList<AnalyticsDesignDto> Designs);

public sealed record CalendarEventDto(DateOnly Date, string NameEn, string NameAr, string Kind);

public sealed record SellerCodeDto(long Id, string Code, string DiscountType, decimal Value, decimal MinSpend, int? MaxUses, int? PerCustomer, int UsesCount,
    DateOnly StartsOn, DateOnly? EndsOn, string Status, string? Label);

/// <summary>A store discount code (paid by the store, only its items). DiscountType: Percent, Fixed, FreeDelivery.</summary>
public sealed record SellerCodeRequest(string Code, string DiscountType, decimal Value, decimal MinSpend, int? MaxUses, int? PerCustomer, DateOnly StartsOn,
    DateOnly? EndsOn, string? Label, bool Active = true);

public sealed record KycStatusDto(string Status, string? RejectReason, DateTime? SubmittedAt);

public sealed record KycRequest(string NationalId);

/// <summary>A design in the review queue (lane: Priority for Pro, Normal; SpotCheck = an Elite design already live).</summary>
public sealed record ReviewDesignDto(long Id, string Title, string? Description, string StoreName, string StoreHandle, string Level, string Lane, string ProductName,
    string? CategoryKey, decimal Price, IReadOnlyList<string> Colors, IReadOnlyList<string> Tags, PhotoDto? FrontArt, PhotoDto? BackArt, PlacementDto? Front,
    PlacementDto? Back, string Status, bool SpotCheck, DateTime? SubmittedAt);

public sealed record RejectRequest(string Reason);

public sealed record DesignReportDto(long Id, long DesignId, string DesignTitle, string StoreName, string Reason, string? Detail, string? Contact, int OpenReports, DateTime CreatedAt, PhotoDto? Art);

/// <summary>Take a design down; a copyright strike freezes the level for 90 days and the third active strike suspends the seller.</summary>
public sealed record TakeDownRequest(string Reason, bool Strike);
