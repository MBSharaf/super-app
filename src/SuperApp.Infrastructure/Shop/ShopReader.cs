using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Read-only queries for the public shop list, product and design pages.</summary>
public sealed partial class ShopReader(AppDbContext db) : IShopReader
{
    private sealed record ProductRow(long Id, string Slug, string Name, string? Brand, decimal Price, decimal? CompareAt, decimal Rating,
        string StoreHandle, string StoreName, StoreType StoreType, string CategoryKey, DateTime CreatedAt,
        string? PhotoContainer, string? PhotoKey, long? PhotoId, int Available, List<string> Sizes, List<string> Colors);

    private sealed record DesignRow(long Id, string Title, string ProductName, decimal Price, decimal SellerRating, string StoreHandle, string StoreName,
        string? CategoryKey, List<string> Tags, List<string> Colors, List<string> Sizes, string? FrontContainer, string? FrontKey, long? FrontId,
        string? BackContainer, string? BackKey, long? BackId, int Sales, int Views, byte Boost, bool Featured, bool FeaturedByAdmin, DateTime CreatedAt, DesignStatus Status);

    public async Task<IReadOnlyList<ShopCardDto>> ProductCardsAsync(ShopQuery query, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(query.Tag))
        {
            return [];
        }
        var rows = await ProductRows(FilterProducts(ShopQueries.LiveProducts(db), query)).ToListAsync(ct);
        return rows.Select(ToCard).ToList();
    }

    public async Task<IReadOnlyList<ShopCardDto>> DesignCardsAsync(ShopQuery query, CancellationToken ct)
    {
        if (!await ShopQueries.PrintingOpenAsync(db, ct))
        {
            return [];
        }
        var rows = await DesignRows(FilterDesigns(ShopQueries.LiveDesigns(db), query)).ToListAsync(ct);
        return rows.Select(ToCard).ToList();
    }

    private static IQueryable<Product> FilterProducts(IQueryable<Product> q, ShopQuery f)
    {
        if (!string.IsNullOrEmpty(f.Q))
        {
            q = q.Where(p => p.Name.Contains(f.Q) || (p.Brand != null && p.Brand.Contains(f.Q)) || p.Category.NameEn.Contains(f.Q)
                || p.Category.NameAr.Contains(f.Q) || p.Store.Name.Contains(f.Q));
        }
        if (!string.IsNullOrEmpty(f.Category)) { q = q.Where(p => p.Category.Key == f.Category); }
        if (!string.IsNullOrEmpty(f.Store)) { q = q.Where(p => p.Store.Handle == f.Store); }
        if (f.MinPrice is not null) { q = q.Where(p => p.Price >= f.MinPrice); }
        if (f.MaxPrice is not null) { q = q.Where(p => p.Price <= f.MaxPrice); }
        return q;
    }

    private static IQueryable<Design> FilterDesigns(IQueryable<Design> q, ShopQuery f)
    {
        if (!string.IsNullOrEmpty(f.Q))
        {
            q = q.Where(d => d.Title.Contains(f.Q) || d.Seller.Store.Name.Contains(f.Q) || d.CatalogProduct.NameEn.Contains(f.Q) || d.Tags.Any(t => t.Tag.Contains(f.Q)));
        }
        if (!string.IsNullOrEmpty(f.DesignCategory)) { q = q.Where(d => d.Category != null && d.Category.Key == f.DesignCategory); }
        if (!string.IsNullOrEmpty(f.Store)) { q = q.Where(d => d.Seller.Store.Handle == f.Store); }
        if (!string.IsNullOrEmpty(f.Tag)) { q = q.Where(d => d.Tags.Any(t => t.Tag == f.Tag)); }
        if (f.MinPrice is not null) { q = q.Where(d => d.Price >= f.MinPrice); }
        if (f.MaxPrice is not null) { q = q.Where(d => d.Price <= f.MaxPrice); }
        return q;
    }

    private static IQueryable<ProductRow> ProductRows(IQueryable<Product> q) => q.AsNoTracking().Select(p => new ProductRow(
        p.Id, p.Slug, p.Name, p.Brand, p.Price, p.CompareAtPrice, p.Rating, p.Store.Handle, p.Store.Name, p.Store.Type, p.Category.Key, p.CreatedAt,
        p.Images.OrderBy(i => i.SortOrder).Select(i => i.File.Container).FirstOrDefault(),
        p.Images.OrderBy(i => i.SortOrder).Select(i => i.File.StorageKey).FirstOrDefault(),
        p.Images.OrderBy(i => i.SortOrder).Select(i => (long?)i.FileId).FirstOrDefault(),
        p.Variants.Sum(v => v.Stock - v.Reserved),
        p.Variants.Select(v => v.Size).Distinct().ToList(),
        p.Variants.Select(v => v.ColorName).Distinct().ToList()));

    private static IQueryable<DesignRow> DesignRows(IQueryable<Design> q) => q.AsNoTracking().Select(d => new DesignRow(
        d.Id, d.Title, d.CatalogProduct.NameEn, d.Price, d.Seller.Rating, d.Seller.Store.Handle, d.Seller.Store.Name,
        d.Category != null ? d.Category.Key : null, d.Tags.Select(t => t.Tag).ToList(),
        d.Colors.Where(c => c.CatalogColor.IsActive).OrderBy(c => c.CatalogColor.SortOrder).Select(c => c.CatalogColor.NameEn).ToList(),
        d.CatalogProduct.Sizes.OrderBy(s => s.SortOrder).Select(s => s.Size).ToList(),
        d.FrontArtFile != null ? d.FrontArtFile.Container : null, d.FrontArtFile != null ? d.FrontArtFile.StorageKey : null, d.FrontArtFileId,
        d.BackArtFile != null ? d.BackArtFile.Container : null, d.BackArtFile != null ? d.BackArtFile.StorageKey : null, d.BackArtFileId,
        d.Sales, d.Views, d.Seller.Level.SearchBoost, d.Seller.FeaturedDesignId == d.Id, d.Seller.FeaturedByAdmin,
        d.ReviewedAt ?? d.CreatedAt, d.Status));

    private static ShopCardDto ToCard(ProductRow r) => new(
        "product", r.Slug, r.Slug, null, r.Name, r.Brand, r.Price, r.CompareAt, r.Rating, ShopQueries.Photo(r.PhotoContainer, r.PhotoKey, r.PhotoId),
        new StoreRefDto(r.StoreHandle, r.StoreName, r.StoreType.ToString()), r.CategoryKey, null, [], r.Sizes, r.Colors, r.Available <= 0,
        ShopQueries.ProductRank(r.Rating, r.StoreType == StoreType.Official), r.CreatedAt);

    private static ShopCardDto ToCard(DesignRow r)
    {
        var image = ShopQueries.Art(r.Id, "front", r.FrontContainer, r.FrontKey, r.FrontId) ?? ShopQueries.Art(r.Id, "back", r.BackContainer, r.BackKey, r.BackId);
        return new ShopCardDto(
            "design", r.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), null, r.Id, $"{r.Title} â€” {r.ProductName}", r.StoreName, r.Price, null,
            r.SellerRating, image, new StoreRefDto(r.StoreHandle, r.StoreName, nameof(StoreType.Seller)), ShopRules.DesignsCategory, r.CategoryKey,
            r.Tags, r.Sizes, r.Colors, false, ShopQueries.DesignRank(r.Sales, r.Views, r.SellerRating, r.Featured, r.FeaturedByAdmin, r.Boost), r.CreatedAt);
    }

    public Task<bool> HasOpenReportAsync(long designId, Guid userId, CancellationToken ct) =>
        db.Set<DesignReport>().AnyAsync(r => r.DesignId == designId && r.ReporterUserId == userId && r.Status == DesignReportStatus.Open, ct);

    public void AddReport(DesignReport report) => db.Set<DesignReport>().Add(report);

    public Task CountViewAsync(long designId, CancellationToken ct) =>
        db.Set<Design>().Where(d => d.Id == designId).ExecuteUpdateAsync(u => u.SetProperty(d => d.Views, d => d.Views + 1), ct);
}
