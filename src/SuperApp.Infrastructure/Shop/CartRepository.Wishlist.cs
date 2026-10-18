using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Shop;

/// <summary>The wishlist: saved products and designs, shown as shop cards while they are on sale.</summary>
public sealed partial class CartRepository
{
    public async Task<IReadOnlyList<WishlistItemDto>> WishlistAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.Set<WishlistItem>().AsNoTracking().Where(w => w.UserId == userId).OrderByDescending(w => w.Id)
            .Select(w => new { w.Id, w.ProductId, w.DesignId, Slug = w.Product != null ? w.Product.Slug : null }).ToListAsync(ct);
        var productIds = rows.Where(r => r.ProductId is not null).Select(r => r.ProductId!.Value).ToList();
        var designIds = rows.Where(r => r.DesignId is not null).Select(r => r.DesignId!.Value).ToList();
        var products = (await ShopReader.ProductRows(ShopQueries.LiveProducts(db).Where(p => productIds.Contains(p.Id))).ToListAsync(ct)).ToDictionary(p => p.Id);
        var printing = designIds.Count == 0 || await ShopQueries.PrintingOpenAsync(db, ct);
        var designs = printing
            ? (await ShopReader.DesignRows(ShopQueries.LiveDesigns(db).Where(d => designIds.Contains(d.Id))).ToListAsync(ct)).ToDictionary(d => d.Id)
            : [];
        return rows.Select(r => r.ProductId is { } p
            ? new WishlistItemDto(r.Id, "product", r.Slug ?? "", products.TryGetValue(p, out var pr) ? ShopReader.ToCard(pr) : null)
            : new WishlistItemDto(r.Id, "design", r.DesignId!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                designs.TryGetValue(r.DesignId.Value, out var dr) ? ShopReader.ToCard(dr) : null)).ToList();
    }

    public async Task<bool> AddWishAsync(Guid userId, string? productSlug, long? designId, CancellationToken ct)
    {
        long? productId = productSlug is null ? null : await db.Set<Product>().Where(p => p.Slug == productSlug).Select(p => (long?)p.Id).FirstOrDefaultAsync(ct);
        var exists = productId is not null || (designId is not null && await db.Set<Design>().AnyAsync(d => d.Id == designId, ct));
        if (!exists)
        {
            return false;
        }
        if (!await db.Set<WishlistItem>().AnyAsync(w => w.UserId == userId && w.ProductId == productId && w.DesignId == (productId == null ? designId : null), ct))
        {
            db.Set<WishlistItem>().Add(new WishlistItem { UserId = userId, ProductId = productId, DesignId = productId is null ? designId : null });
            await db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task<bool> RemoveWishAsync(Guid userId, long id, CancellationToken ct) =>
        await db.Set<WishlistItem>().Where(w => w.Id == id && w.UserId == userId).ExecuteDeleteAsync(ct) > 0;
}
