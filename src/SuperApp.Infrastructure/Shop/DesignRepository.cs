using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Pod;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>The printable catalogue and sellers' designs.</summary>
public sealed class DesignRepository(AppDbContext db) : IDesignRepository, ISellerLevels
{
    public async Task<IReadOnlyList<SellerCatalogDto>> CatalogueAsync(decimal levelDiscount, CancellationToken ct)
    {
        var products = await Catalogue().Where(c => c.Status == CatalogProductStatus.Active).OrderBy(c => c.Id).ToListAsync(ct);
        return products.Select(c => new SellerCatalogDto(c.Id, c.Key, c.NameEn, c.NameAr, c.Kind.ToString(), c.DescriptionEn, c.DescriptionAr, c.BasePrice,
            c.BasePrice - levelDiscount, c.BackPrintExtra, c.BigSizeExtra, c.MaxPrice, c.HasBackPrint, c.Model3dUrl, c.Status.ToString(),
            c.Colors.Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => new CatalogColorDto(x.Id, x.NameEn, x.NameAr, x.Hex)).ToList(),
            c.Sizes.OrderBy(s => s.SortOrder).Select(s => new CatalogSizeDto(s.Size, s.IsBigSize, s.ChestCm, s.LengthCm, s.SleeveCm)).ToList(),
            c.Details.OrderBy(d => d.SortOrder).Select(d => new CatalogDetailDto(d.LabelEn, d.LabelAr, d.ValueEn, d.ValueAr)).ToList(),
            c.PrintAreas.OrderBy(a => a.Side).Select(a => new PrintAreaDto(a.Side.ToString(), a.X, a.Y, a.Width, a.Height, a.PrintWidthCm)).ToList())).ToList();
    }

    private IQueryable<CatalogProduct> Catalogue() => db.Set<CatalogProduct>().AsNoTracking()
        .Include(c => c.Colors).Include(c => c.Sizes).Include(c => c.Details).Include(c => c.PrintAreas).AsSplitQuery();

    public Task<CatalogProduct?> CatalogProductAsync(int id, CancellationToken ct) =>
        Catalogue().FirstOrDefaultAsync(c => c.Id == id && c.Status != CatalogProductStatus.Retired, ct);

    public async Task<IReadOnlyList<SellerDesignDto>> DesignsAsync(long sellerId, string? status, decimal levelDiscount, CancellationToken ct)
    {
        var q = Designs().Where(d => d.SellerId == sellerId);
        if (status is not null)
        {
            var s = Enum.Parse<DesignStatus>(status, true);
            q = q.Where(d => d.Status == s);
        }
        return (await q.OrderByDescending(d => d.CreatedAt).ToListAsync(ct)).Select(d => ToDto(d, levelDiscount)).ToList();
    }

    public async Task<SellerDesignDto?> DesignDtoAsync(long sellerId, long id, decimal levelDiscount, CancellationToken ct)
    {
        var d = await Designs().FirstOrDefaultAsync(x => x.SellerId == sellerId && x.Id == id, ct);
        return d is null ? null : ToDto(d, levelDiscount);
    }

    private IQueryable<Design> Designs() => db.Set<Design>().AsNoTracking().Include(d => d.CatalogProduct).Include(d => d.Category)
        .Include(d => d.Colors).ThenInclude(c => c.CatalogColor).Include(d => d.Tags).Include(d => d.FrontArtFile).Include(d => d.BackArtFile).AsSplitQuery();

    private static SellerDesignDto ToDto(Design d, decimal levelDiscount)
    {
        var back = d.BackArtFileId is not null && d.CatalogProduct.HasBackPrint;
        var basePrice = DesignRules.Base(d.CatalogProduct, back, levelDiscount);
        return new SellerDesignDto(d.Id, d.CatalogProductId, d.CatalogProduct.Key, d.CatalogProduct.NameEn, d.Category?.Key, d.Title, d.Description, d.Price, basePrice,
            DesignRules.Max(d.CatalogProduct, back), d.Price - basePrice, d.Colors.Select(c => c.CatalogColorId).ToList(), d.Colors.Select(c => c.CatalogColor.NameEn).ToList(),
            d.Tags.Select(t => t.Tag).ToList(), ShopQueries.Art(d.Id, "front", d.FrontArtFile?.Container, d.FrontArtFile?.StorageKey, d.FrontArtFileId),
            ShopQueries.Art(d.Id, "back", d.BackArtFile?.Container, d.BackArtFile?.StorageKey, d.BackArtFileId),
            d.FrontX is null ? null : new PlacementDto(d.FrontX.Value, d.FrontY ?? 0, d.FrontW ?? 0), d.BackX is null ? null : new PlacementDto(d.BackX.Value, d.BackY ?? 0, d.BackW ?? 0),
            d.Status.ToString(), d.RejectReason, d.SpotCheck, d.Views, d.Sales, d.SubmittedAt, d.ReviewedAt, d.CreatedAt, DesignRules.Token(d));
    }

    public Task<Design?> DesignAsync(long sellerId, long id, CancellationToken ct) =>
        db.Set<Design>().Include(d => d.Colors).Include(d => d.Tags).AsSplitQuery().FirstOrDefaultAsync(d => d.SellerId == sellerId && d.Id == id, ct);

    public void AddDesign(Design design) => db.Set<Design>().Add(design);

    public Task<int?> DesignCategoryIdAsync(string key, CancellationToken ct) =>
        db.Set<DesignCategory>().Where(c => c.Key == key).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);

    public Task<int> ActiveDesignsAsync(long sellerId, CancellationToken ct) =>
        db.Set<Design>().CountAsync(d => d.SellerId == sellerId && (d.Status == DesignStatus.Live || d.Status == DesignStatus.InReview || d.Status == DesignStatus.Paused), ct);

    public Task ClearFeaturedAsync(long sellerId, long designId, CancellationToken ct) =>
        db.Set<Seller>().Where(s => s.Id == sellerId && s.FeaturedDesignId == designId).ExecuteUpdateAsync(u => u.SetProperty(s => s.FeaturedDesignId, (long?)null), ct);

    public Task<SellerLevel?> NextAsync(byte rank, CancellationToken ct) =>
        db.Set<SellerLevel>().AsNoTracking().Where(l => l.Rank > rank).OrderBy(l => l.Rank).FirstOrDefaultAsync(ct);
}
