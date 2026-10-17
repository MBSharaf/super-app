using SuperApp.Application.Features.Pitches;

namespace SuperApp.Application.Features.Shop.Catalog;

/// <summary>
/// GET /shop/products filters (API §4.1). One list of store products and live printed designs.
/// <c>Category</c> = product category key ("designs" = printed designs only), <c>DesignCategory</c> = design category key,
/// <c>Store</c> = store handle, <c>Kind</c> = product | design. <c>Sort</c>: featured (default), price, -price, rating, newest.
/// </summary>
public sealed record ShopQuery(
    string? Q = null,
    string? Category = null,
    string? DesignCategory = null,
    string? Store = null,
    string? Tag = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Kind = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = 24);

/// <summary>The store that sells an item ("Sold by", FR-SHOP-11). Type: Official, PitchPartner, Seller.</summary>
public sealed record StoreRefDto(string Handle, string Name, string Type);

/// <summary>
/// One item in the shop list. <c>Kind</c> is "product" (Id = slug) or "design" (Id = design id). Demo photos carry only
/// their <c>Key</c>; uploads have a <c>Url</c>. <c>Rank</c> is the "featured" score (sales, views, rating, seller level).
/// </summary>
public sealed record ShopCardDto(
    string Kind,
    string Id,
    string? Slug,
    long? DesignId,
    string Name,
    string? Brand,
    decimal Price,
    decimal? CompareAtPrice,
    decimal Rating,
    PhotoDto? Image,
    StoreRefDto Store,
    string? CategoryKey,
    string? DesignCategoryKey,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Sizes,
    IReadOnlyList<string> Colors,
    bool SoldOut,
    double Rank,
    DateTime CreatedAt);

/// <summary>A variant the customer picks (size and colour). <c>Available</c> = stock not held by other checkouts.</summary>
public sealed record VariantDto(long Id, string Size, string ColorName, string? ColorHex, string Sku, int Available, decimal Price);

public sealed record SpecDto(string Label, string Value);

/// <summary>GET /shop/products/{slug} — the product page.</summary>
public sealed record ProductDetailsDto(
    ShopCardDto Card,
    string? Description,
    string CategoryNameEn,
    string CategoryNameAr,
    IReadOnlyList<PhotoDto> Images,
    IReadOnlyList<VariantDto> Variants,
    IReadOnlyList<SpecDto> Specs);

public sealed record CatalogColorDto(long Id, string NameEn, string NameAr, string Hex);

/// <summary>A size and its chart (cm, measured flat). Big sizes cost the catalogue's big-size extra.</summary>
public sealed record CatalogSizeDto(string Size, bool IsBigSize, decimal ChestCm, decimal LengthCm, decimal SleeveCm);

public sealed record CatalogDetailDto(string LabelEn, string LabelAr, string ValueEn, string ValueAr);

/// <summary>Where art may go on the garment (fractions of the 400×440 template).</summary>
public sealed record PrintAreaDto(string Side, decimal X, decimal Y, decimal Width, decimal Height, decimal PrintWidthCm);

/// <summary>Where the seller placed the art: centre X / Y and width, as fractions of the template.</summary>
public sealed record PlacementDto(decimal X, decimal Y, decimal W);

/// <summary>GET /shop/designs/{id} — the printed-design page.</summary>
public sealed record DesignDetailsDto(
    ShopCardDto Card,
    string? Description,
    string CatalogKey,
    string ProductNameEn,
    string ProductNameAr,
    string GarmentKind,
    decimal BigSizeExtra,
    string? Model3dUrl,
    IReadOnlyList<CatalogColorDto> Colors,
    IReadOnlyList<CatalogSizeDto> Sizes,
    IReadOnlyList<CatalogDetailDto> Details,
    IReadOnlyList<PrintAreaDto> PrintAreas,
    PhotoDto? FrontArt,
    PhotoDto? BackArt,
    PlacementDto? Front,
    PlacementDto? Back,
    string? SellerLevel);

/// <summary>A design on its store page; the owner also sees designs that are not public yet, with their status.</summary>
public sealed record StoreDesignDto(ShopCardDto Card, string Status);

/// <summary>GET /stores/{handle} — the store page.</summary>
public sealed record StorePageDto(
    string Handle,
    string Name,
    string Type,
    string? Tagline,
    string? About,
    PhotoDto? Logo,
    PhotoDto? Cover,
    int CoverPositionY,
    string AccentColor,
    string Status,
    DateOnly? BreakUntil,
    string? BreakNote,
    string? Level,
    decimal? Rating,
    int Followers,
    bool IsFollowing,
    bool IsOwner,
    long? FeaturedDesignId,
    IReadOnlyList<ShopCardDto> Products,
    IReadOnlyList<StoreDesignDto> Designs);

/// <summary>POST /shop/designs/{id}/report. Reason: Copyright, Trademark, Offensive, Other.</summary>
public sealed record ReportDesignRequest(string Reason, string? Detail, string? Contact);

/// <summary>Who may see a design's art when it is not live: its seller and the reviewers.</summary>
public sealed record DesignAccess(long DesignId, Guid SellerUserId, bool IsLive);

/// <summary>A design picture to send to the browser (resized preview, or the SVG itself).</summary>
public sealed record ArtImage(byte[] Bytes, string ContentType);
