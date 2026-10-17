using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Which products and designs the public may see, shared by the shop, the cart and checkout.</summary>
internal static class ShopQueries
{
    /// <summary>Published, not hidden by an admin, its category shown, and its store open (not on a break or suspended).</summary>
    public static IQueryable<Product> LiveProducts(AppDbContext db) => db.Set<Product>().Where(p =>
        p.Status == ProductStatus.Published && !p.HiddenByAdmin && !p.Category.IsHidden && p.Store.Status == StoreStatus.Active);

    /// <summary>Live designs of active sellers whose store is open and whose printable product is on sale.</summary>
    public static IQueryable<Design> LiveDesigns(AppDbContext db) => db.Set<Design>().Where(d =>
        d.Status == DesignStatus.Live && d.Seller.Status != SellerStatus.Suspended && d.Seller.Store.Status == StoreStatus.Active
        && d.CatalogProduct.Status == CatalogProductStatus.Active);

    /// <summary>
    /// Printing is open unless every factory has paused its intake (FR-FAC-08). With no factory at all designs stay on
    /// sale and checkout refuses printed items until one exists.
    /// </summary>
    public static async Task<bool> PrintingOpenAsync(AppDbContext db, CancellationToken ct) =>
        !await db.Set<Factory>().AnyAsync(ct) || await AcceptingFactoryAsync(db, ct) is not null;

    /// <summary>The active factory with intake open and the fewest open jobs (null when none can take work).</summary>
    public static Task<Factory?> AcceptingFactoryAsync(AppDbContext db, CancellationToken ct) =>
        db.Set<Factory>().Where(f => f.Status == FactoryStatus.Active && !f.IntakePaused)
            .OrderBy(f => db.Set<Job>().Count(j => j.FactoryId == f.Id && j.Stage < JobStage.Ready)).ThenBy(f => f.Id)
            .FirstOrDefaultAsync(ct);

    /// <summary>Demo photos are external keys (the site builds their address); uploads are served by /files.</summary>
    public static PhotoDto? Photo(string? container, string? key, long? fileId) =>
        fileId is null ? null : container!.StartsWith("demo", StringComparison.Ordinal) ? new PhotoDto(null, key) : new PhotoDto($"/api/v1/files/{fileId}", null);

    /// <summary>Design art: demo art is a key; uploaded art is a resized preview served by the shop.</summary>
    public static PhotoDto? Art(long designId, string side, string? container, string? key, long? fileId) =>
        fileId is null ? null : container!.StartsWith("demo", StringComparison.Ordinal) ? new PhotoDto(null, key) : new PhotoDto($"/api/v1/shop/designs/{designId}/art/{side}", null);

    /// <summary>"Featured" score of a design: sales, views, seller rating, featured flags, lifted by the seller's level.</summary>
    public static double DesignRank(int sales, int views, decimal rating, bool featured, bool featuredByAdmin, byte boost) =>
        ((sales * 6) + (views / 60.0) + ((double)(rating > 0 ? rating : 4m) * 8) + (featured ? 15 : 0) + (featuredByAdmin ? 25 : 0)) * (1 + (boost * 0.25)) + (boost * 30);

    /// <summary>"Featured" score of a store product (same scale as designs).</summary>
    public static double ProductRank(decimal rating, bool official) => ((double)rating * 20) + (official ? 10 : 0);
}
