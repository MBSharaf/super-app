using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>Demo 15: the 3 print-on-demand sellers (level from their sales and rating) and their 11 designs with colours and tags.</summary>
public sealed class DemoPodSeeder(AppDbContext db) : ISeeder
{
    private sealed record SellerRow(string Key, string User, string Store, string Level, string Mode, int Sales, decimal Rating, string PayoutMethod, string PayoutDetail);
    private sealed record Placement(decimal X, decimal Y, decimal W);
    private sealed record DesignRow(string Key, string Seller, string Catalogue, string? Category, string Title, string? Description, decimal Price,
        string Status, string? FrontArt, string? BackArt, Placement? Front, Placement? Back, List<string> Colors, List<string> Tags,
        int Views, int Sales, DateTime? SubmittedAt, DateTime? ReviewedAt);
    private sealed record PodFile(List<SellerRow> Sellers, List<DesignRow> Designs);

    public int Order => 15;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var file = SeedFile.Read<PodFile>("Demo/pod.json");
        var users = await DemoRefs.UsersAsync(db, ct);
        var stores = await db.Set<Store>().ToDictionaryAsync(s => s.Handle, s => s.Id, ct);
        var levels = await db.Set<SellerLevel>().ToDictionaryAsync(l => l.Key, l => l.Id, ct);
        var sellers = file.Sellers.Select(s => new Seller
        {
            UserId = users[s.User], StoreId = stores[s.Store], LevelId = levels[s.Level], Mode = DemoRefs.Parse<SellerMode>(s.Mode), Status = SellerStatus.Active,
            LifetimeSales = s.Sales, Rating = s.Rating, PayoutMethod = DemoRefs.Parse<PayoutMethod>(s.PayoutMethod), PayoutDetail = s.PayoutDetail,
        });
        var added = await db.Set<Seller>().AddMissingAsync(sellers, s => s.UserId, ct);
        await db.SaveChangesAsync(ct);

        var sellerIds = file.Sellers.ToDictionary(s => s.Key, s => users[s.User]);
        var existing = await db.Set<Design>().IgnoreQueryFilters().Select(d => d.Title).ToListAsync(ct);
        foreach (var row in file.Designs.Where(d => !existing.Contains(d.Title)))
        {
            var sellerId = await db.Set<Seller>().Where(s => s.UserId == sellerIds[row.Seller]).Select(s => s.Id).SingleAsync(ct);
            await AddDesignAsync(row, sellerId, ct);
            added++;
        }
        return added;
    }

    private async Task AddDesignAsync(DesignRow row, long sellerId, CancellationToken ct)
    {
        var files = await DemoRefs.FilesAsync(db, ct);
        var catalogueId = await db.Set<CatalogProduct>().Where(c => c.Key == row.Catalogue).Select(c => c.Id).SingleAsync(ct);
        var design = new Design
        {
            SellerId = sellerId, CatalogProductId = catalogueId, Title = row.Title, Description = row.Description, Price = row.Price,
            CategoryId = await db.Set<DesignCategory>().Where(c => c.Key == row.Category).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct),
            FrontArtFileId = row.FrontArt is null ? null : files[row.FrontArt], BackArtFileId = row.BackArt is null ? null : files[row.BackArt],
            FrontX = row.Front?.X, FrontY = row.Front?.Y, FrontW = row.Front?.W, BackX = row.Back?.X, BackY = row.Back?.Y, BackW = row.Back?.W,
            Status = DemoRefs.Parse<DesignStatus>(row.Status), Views = row.Views, Sales = row.Sales, SubmittedAt = row.SubmittedAt, ReviewedAt = row.ReviewedAt,
        };
        db.Set<Design>().Add(design);
        await db.SaveChangesAsync(ct);

        var colors = await db.Set<CatalogColor>().Where(c => c.CatalogProductId == catalogueId && row.Colors.Contains(c.NameEn)).Select(c => c.Id).ToListAsync(ct);
        db.Set<DesignColor>().AddRange(colors.Select(id => new DesignColor { DesignId = design.Id, CatalogColorId = id }));
        db.Set<DesignTag>().AddRange(row.Tags.Distinct().Select(t => new DesignTag { DesignId = design.Id, Tag = t }));
        await db.SaveChangesAsync(ct);
    }
}
