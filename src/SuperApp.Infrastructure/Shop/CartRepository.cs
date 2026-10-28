using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>The cart with live prices and stock, the wishlist, and promo codes.</summary>
public sealed partial class CartRepository(AppDbContext db) : ICartRepository
{
    private sealed record VariantRow(long Id, string Slug, string Name, string Size, string Color, int Available, decimal Price, decimal? CompareAt,
        long StoreId, string StoreHandle, string StoreName, StoreType StoreType, Guid? StoreOwner, bool Live, string? PhotoContainer, string? PhotoKey, long? PhotoId);

    private sealed record DesignRowInfo(long Id, string Title, string ProductName, decimal Price, long SellerId, Guid SellerUserId, long StoreId, string StoreHandle,
        string StoreName, Guid? StoreOwner, int CatalogId, decimal BasePrice, decimal BackExtra, decimal BigExtra, decimal FactoryCost, bool Back, decimal LevelDiscount,
        bool Live, List<string> Colors, List<string> BigSizes, string? ArtContainer, string? ArtKey, long? ArtId);

    public async Task<IReadOnlyList<CartLine>> LinesAsync(Guid userId, CancellationToken ct)
    {
        var items = await db.Set<CartItem>().AsNoTracking().Where(c => c.UserId == userId).OrderBy(c => c.Id).ToListAsync(ct);
        var variantIds = items.Where(i => i.ProductVariantId is not null).Select(i => i.ProductVariantId!.Value).ToList();
        var designIds = items.Where(i => i.DesignId is not null).Select(i => i.DesignId!.Value).ToList();
        var variants = await VariantRows(db.Set<ProductVariant>().Where(v => variantIds.Contains(v.Id))).ToDictionaryAsync(v => v.Id, ct);
        var designs = await DesignRows(db.Set<Design>().Where(d => designIds.Contains(d.Id))).ToDictionaryAsync(d => d.Id, ct);
        var rates = await RatesAsync(variants.Values.Select(v => (v.StoreId, v.StoreType, v.StoreOwner)).Distinct().ToList(), ct);
        var printing = designs.Count == 0 || await ShopQueries.PrintingOpenAsync(db, ct);
        return items.Select(i => i.ProductVariantId is { } v
            ? FromVariant(i, variants.GetValueOrDefault(v), rates)
            : FromDesign(i, designs.GetValueOrDefault(i.DesignId!.Value), printing)).ToList();
    }

    private static IQueryable<VariantRow> VariantRows(IQueryable<ProductVariant> q) => q.AsNoTracking().Select(v => new VariantRow(
        v.Id, v.Product.Slug, v.Product.Name, v.Size, v.ColorName, v.Stock - v.Reserved, v.PriceOverride ?? v.Product.Price, v.Product.CompareAtPrice,
        v.Product.StoreId, v.Product.Store.Handle, v.Product.Store.Name, v.Product.Store.Type, v.Product.Store.OwnerUserId,
        v.Product.Status == ProductStatus.Published && !v.Product.HiddenByAdmin && !v.Product.Category.IsHidden && v.Product.Store.Status == StoreStatus.Active,
        v.Product.Images.OrderBy(i => i.SortOrder).Select(i => i.File.Container).FirstOrDefault(),
        v.Product.Images.OrderBy(i => i.SortOrder).Select(i => i.File.StorageKey).FirstOrDefault(),
        v.Product.Images.OrderBy(i => i.SortOrder).Select(i => (long?)i.FileId).FirstOrDefault()));

    private static IQueryable<DesignRowInfo> DesignRows(IQueryable<Design> q) => q.AsNoTracking().Select(d => new DesignRowInfo(
        d.Id, d.Title, d.CatalogProduct.NameEn, d.Price, d.SellerId, d.Seller.UserId, d.Seller.StoreId, d.Seller.Store.Handle, d.Seller.Store.Name,
        d.Seller.Store.OwnerUserId, d.CatalogProductId, d.CatalogProduct.BasePrice, d.CatalogProduct.BackPrintExtra, d.CatalogProduct.BigSizeExtra,
        d.CatalogProduct.FactoryCost, d.BackArtFileId != null, d.Seller.Level.BaseDiscount,
        d.Status == DesignStatus.Live && d.Seller.Status != SellerStatus.Suspended && d.Seller.Store.Status == StoreStatus.Active && d.CatalogProduct.Status == CatalogProductStatus.Active,
        d.Colors.Where(c => c.CatalogColor.IsActive).Select(c => c.CatalogColor.NameEn).ToList(),
        d.CatalogProduct.Sizes.Where(s => s.IsBigSize).Select(s => s.Size).ToList(),
        d.FrontArtFile != null ? d.FrontArtFile.Container : d.BackArtFile != null ? d.BackArtFile.Container : null,
        d.FrontArtFile != null ? d.FrontArtFile.StorageKey : d.BackArtFile != null ? d.BackArtFile.StorageKey : null,
        d.FrontArtFileId ?? d.BackArtFileId));

    /// <summary>
    /// Commission rate per store (fraction) on its own products: a partner's own deal (stored as percent), else null = the
    /// store rate (commission.store, 5% — owner's decision 2026-10-27). Sellers' levels price printed designs, not own products.
    /// </summary>
    private async Task<Dictionary<long, decimal?>> RatesAsync(List<(long StoreId, StoreType Type, Guid? Owner)> stores, CancellationToken ct)
    {
        var owners = stores.Where(s => s.Type == StoreType.PitchPartner && s.Owner is not null).Select(s => s.Owner!.Value).ToList();
        var partners = await db.Set<Partner>().AsNoTracking().Where(p => owners.Contains(p.UserId)).ToDictionaryAsync(p => p.UserId, p => p.StoreCommission, ct);
        return stores.ToDictionary(s => s.StoreId, s => s.Type switch
        {
            StoreType.PitchPartner => s.Owner is { } o && partners.GetValueOrDefault(o) is { } pct ? pct / 100 : null,
            StoreType.Seller => null,
            _ => (decimal?)0,
        });
    }

    private static CartLine FromVariant(CartItem i, VariantRow? v, Dictionary<long, decimal?> rates)
    {
        if (v is null)
        {
            return Gone(i, OrderItemKind.StoreProduct);
        }
        var problem = !v.Live ? "This item isn’t on sale any more." : v.Available <= 0 ? "Sold out." : v.Available < i.Qty ? $"Only {v.Available} left." : null;
        return new CartLine(i.Id, OrderItemKind.StoreProduct, v.Slug, v.Id, null, v.Name, ShopQueries.Photo(v.PhotoContainer, v.PhotoKey, v.PhotoId),
            v.StoreId, v.StoreHandle, v.StoreName, v.StoreType, v.StoreOwner, v.Size, v.Color, i.Qty, v.Price, v.CompareAt, v.Available, problem,
            rates.GetValueOrDefault(v.StoreId), null);
    }

    private static CartLine FromDesign(CartItem i, DesignRowInfo? d, bool printing)
    {
        if (d is null)
        {
            return Gone(i, OrderItemKind.PrintedDesign);
        }
        var big = d.BigSizes.Contains(i.Size ?? "");
        var problem = !d.Live ? "This design isn’t on sale any more." : !printing ? "Printing is paused for now."
            : !d.Colors.Contains(i.Color ?? "") ? "This colour isn’t available any more." : null;
        var baseUnit = d.BasePrice + (d.Back ? d.BackExtra : 0) + (big ? d.BigExtra : 0) - d.LevelDiscount;
        var printed = new PrintedLine(d.CatalogId, baseUnit, d.FactoryCost, d.Back, d.SellerId, d.SellerUserId);
        return new CartLine(i.Id, OrderItemKind.PrintedDesign, d.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, d.Id, $"{d.Title} — {d.ProductName}",
            ShopQueries.Art(d.Id, "front", d.ArtContainer, d.ArtKey, d.ArtId), d.StoreId, d.StoreHandle, d.StoreName, StoreType.Seller, d.StoreOwner,
            i.Size ?? "", i.Color ?? "", i.Qty, d.Price + (big ? d.BigExtra : 0), null, 99, problem, null, printed);
    }

    private static CartLine Gone(CartItem i, OrderItemKind kind) => new(
        i.Id, kind, "", i.ProductVariantId, i.DesignId, "Removed item", null, 0, "", "", StoreType.Official, null, i.Size ?? "", i.Color ?? "", i.Qty, 0, null, 0,
        "This item was removed by the store.", null, null);

    public Task<CartItem?> ItemAsync(Guid userId, long id, CancellationToken ct) =>
        db.Set<CartItem>().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

    public Task<CartItem?> SameItemAsync(Guid userId, long? variantId, long? designId, string? size, string? color, CancellationToken ct) =>
        db.Set<CartItem>().FirstOrDefaultAsync(c => c.UserId == userId && c.ProductVariantId == variantId && c.DesignId == designId && c.Size == size && c.Color == color, ct);

    public void Add(CartItem item) => db.Set<CartItem>().Add(item);

    public void Remove(CartItem item) => db.Set<CartItem>().Remove(item);

    public async Task<int?> VariantAvailableAsync(long variantId, CancellationToken ct)
    {
        var row = await VariantRows(db.Set<ProductVariant>().Where(v => v.Id == variantId)).FirstOrDefaultAsync(ct);
        return row is { Live: true } ? row.Available : null;
    }

    public async Task<(IReadOnlyList<string> Sizes, IReadOnlyList<string> Colors)?> DesignOptionsAsync(long designId, CancellationToken ct)
    {
        if (!await ShopQueries.PrintingOpenAsync(db, ct))
        {
            return null;
        }
        var row = await ShopQueries.LiveDesigns(db).Where(d => d.Id == designId).Select(d => new
        {
            Sizes = d.CatalogProduct.Sizes.OrderBy(s => s.SortOrder).Select(s => s.Size).ToList(),
            Colors = d.Colors.Where(c => c.CatalogColor.IsActive).Select(c => c.CatalogColor.NameEn).ToList(),
        }).FirstOrDefaultAsync(ct);
        return row is null ? null : (row.Sizes, row.Colors);
    }

    public Task<PromoCode?> PromoAsync(string code, CancellationToken ct) => db.Set<PromoCode>().AsNoTracking().FirstOrDefaultAsync(p => p.Code == code, ct);

    public Task<int> PromoUsesAsync(long promoId, Guid userId, CancellationToken ct) =>
        db.Set<PromoCodeUse>().CountAsync(u => u.PromoCodeId == promoId && u.UserId == userId, ct);

    public Task<string?> StoreHandleAsync(long storeId, CancellationToken ct) =>
        db.Set<Store>().Where(s => s.Id == storeId).Select(s => s.Handle).FirstOrDefaultAsync(ct);
}
