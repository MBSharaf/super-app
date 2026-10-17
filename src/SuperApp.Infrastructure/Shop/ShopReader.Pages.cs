using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Pitches;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Product, design and store pages.</summary>
public sealed partial class ShopReader
{
    public async Task<ProductDetailsDto?> ProductAsync(string slug, CancellationToken ct)
    {
        var live = ShopQueries.LiveProducts(db).Where(p => p.Slug == slug);
        var row = await ProductRows(live).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }
        var product = await db.Set<Product>().AsNoTracking().Where(p => p.Id == row.Id)
            .Include(p => p.Images).ThenInclude(i => i.File).Include(p => p.Variants).Include(p => p.Specs).Include(p => p.Category)
            .AsSplitQuery().SingleAsync(ct);
        return new ProductDetailsDto(
            ToCard(row), product.Description, product.Category.NameEn, product.Category.NameAr,
            product.Images.OrderBy(i => i.SortOrder).Select(i => ShopQueries.Photo(i.File.Container, i.File.StorageKey, i.FileId)!).ToList(),
            product.Variants.OrderBy(v => v.Id).Select(v => new VariantDto(v.Id, v.Size, v.ColorName, v.ColorHex, v.Sku, v.Stock - v.Reserved, v.PriceOverride ?? product.Price)).ToList(),
            product.Specs.OrderBy(s => s.SortOrder).Select(s => new SpecDto(s.Label, s.Value)).ToList());
    }

    public async Task<DesignDetailsDto?> DesignAsync(long id, CancellationToken ct)
    {
        if (!await ShopQueries.PrintingOpenAsync(db, ct))
        {
            return null;
        }
        var row = await DesignRows(ShopQueries.LiveDesigns(db).Where(d => d.Id == id)).FirstOrDefaultAsync(ct);
        return row is null ? null : await DesignDetailsAsync(row, ct);
    }

    private async Task<DesignDetailsDto> DesignDetailsAsync(DesignRow row, CancellationToken ct)
    {
        var d = await db.Set<Design>().AsNoTracking().Where(x => x.Id == row.Id)
            .Include(x => x.CatalogProduct).ThenInclude(c => c.Sizes).Include(x => x.CatalogProduct).ThenInclude(c => c.Details)
            .Include(x => x.CatalogProduct).ThenInclude(c => c.PrintAreas).Include(x => x.Colors).ThenInclude(c => c.CatalogColor)
            .Include(x => x.Seller).ThenInclude(s => s.Level).AsSplitQuery().SingleAsync(ct);
        var c = d.CatalogProduct;
        return new DesignDetailsDto(
            ToCard(row), d.Description, c.Key, c.NameEn, c.NameAr, c.Kind.ToString(), c.BigSizeExtra, c.Model3dUrl,
            d.Colors.Select(x => x.CatalogColor).Where(x => x.IsActive).OrderBy(x => x.SortOrder).Select(x => new CatalogColorDto(x.Id, x.NameEn, x.NameAr, x.Hex)).ToList(),
            c.Sizes.OrderBy(s => s.SortOrder).Select(s => new CatalogSizeDto(s.Size, s.IsBigSize, s.ChestCm, s.LengthCm, s.SleeveCm)).ToList(),
            c.Details.OrderBy(x => x.SortOrder).Select(x => new CatalogDetailDto(x.LabelEn, x.LabelAr, x.ValueEn, x.ValueAr)).ToList(),
            c.PrintAreas.OrderBy(a => a.Side).Select(a => new PrintAreaDto(a.Side.ToString(), a.X, a.Y, a.Width, a.Height, a.PrintWidthCm)).ToList(),
            ShopQueries.Art(d.Id, "front", row.FrontContainer, row.FrontKey, row.FrontId), ShopQueries.Art(d.Id, "back", row.BackContainer, row.BackKey, row.BackId),
            d.FrontX is null ? null : new PlacementDto(d.FrontX.Value, d.FrontY!.Value, d.FrontW!.Value),
            d.BackX is null ? null : new PlacementDto(d.BackX.Value, d.BackY!.Value, d.BackW!.Value),
            d.Seller.Level.Key);
    }

    public async Task<DesignAccess?> DesignAccessAsync(long id, CancellationToken ct)
    {
        var owner = await db.Set<Design>().AsNoTracking().Where(d => d.Id == id).Select(d => (Guid?)d.Seller.UserId).FirstOrDefaultAsync(ct);
        if (owner is null)
        {
            return null;
        }
        var live = await ShopQueries.LiveDesigns(db).AnyAsync(d => d.Id == id, ct) && await ShopQueries.PrintingOpenAsync(db, ct);
        return new DesignAccess(id, owner.Value, live);
    }

    public async Task<(long FileId, string Container, string Key)?> ArtFileAsync(long designId, int side, CancellationToken ct)
    {
        var fileId = await db.Set<Design>().AsNoTracking().Where(d => d.Id == designId)
            .Select(d => side == 1 ? d.BackArtFileId : d.FrontArtFileId).FirstOrDefaultAsync(ct);
        var art = fileId is null ? null : await db.Set<SuperApp.Domain.Entities.Ops.StoredFile>().AsNoTracking()
            .Where(f => f.Id == fileId).Select(f => new { f.Id, f.Container, f.StorageKey }).FirstOrDefaultAsync(ct);
        return art is null ? null : (art.Id, art.Container, art.StorageKey);
    }

    public async Task<(long SellerId, Guid OwnerUserId)?> SellerOfStoreAsync(string handle, CancellationToken ct)
    {
        var s = await db.Set<Seller>().AsNoTracking().Where(s => s.Store.Handle == handle && s.Status != SellerStatus.Suspended && s.Store.Status != StoreStatus.Suspended)
            .Select(s => new { s.Id, s.UserId }).FirstOrDefaultAsync(ct);
        return s is null ? null : (s.Id, s.UserId);
    }

    public async Task<bool> SetFollowAsync(long sellerId, Guid userId, bool follow, CancellationToken ct)
    {
        var row = await db.Set<SellerFollower>().FirstOrDefaultAsync(f => f.SellerId == sellerId && f.UserId == userId, ct);
        if (follow == (row is not null))
        {
            return false;
        }
        if (follow)
        {
            db.Set<SellerFollower>().Add(new SellerFollower { SellerId = sellerId, UserId = userId });
        }
        else
        {
            db.Set<SellerFollower>().Remove(row!);
        }
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<StorePageDto?> StoreAsync(string handle, Guid? caller, CancellationToken ct)
    {
        var store = await db.Set<Store>().AsNoTracking().Include(s => s.LogoFile).Include(s => s.CoverFile).FirstOrDefaultAsync(s => s.Handle == handle, ct);
        var seller = store is null ? null : await db.Set<Seller>().AsNoTracking().Include(s => s.Level).FirstOrDefaultAsync(s => s.StoreId == store.Id, ct);
        var isOwner = store is not null && caller is not null && store.OwnerUserId == caller;
        if (store is null || (!isOwner && (store.Status == StoreStatus.Suspended || seller?.Status == SellerStatus.Suspended)))
        {
            return null;
        }
        var products = await ProductCardsAsync(new ShopQuery(Store: handle), ct);
        var designs = seller is null ? [] : await StoreDesignsAsync(seller.Id, handle, isOwner, ct);
        var followers = seller is null ? 0 : await db.Set<SellerFollower>().CountAsync(f => f.SellerId == seller.Id, ct);
        var following = seller is not null && caller is not null && await db.Set<SellerFollower>().AnyAsync(f => f.SellerId == seller.Id && f.UserId == caller, ct);
        return new StorePageDto(
            store.Handle, store.Name, store.Type.ToString(), store.Tagline, store.About,
            ShopQueries.Photo(store.LogoFile?.Container, store.LogoFile?.StorageKey, store.LogoFileId),
            ShopQueries.Photo(store.CoverFile?.Container, store.CoverFile?.StorageKey, store.CoverFileId),
            store.CoverPositionY, store.AccentColor, store.Status.ToString(), store.BreakUntil, store.BreakNote,
            seller?.Level.Key, seller?.Rating, followers, following, isOwner, seller?.FeaturedDesignId, products, designs);
    }

    private async Task<IReadOnlyList<StoreDesignDto>> StoreDesignsAsync(long sellerId, string handle, bool isOwner, CancellationToken ct)
    {
        if (!isOwner)
        {
            return (await DesignCardsAsync(new ShopQuery(Store: handle), ct)).Select(c => new StoreDesignDto(c, nameof(DesignStatus.Live))).ToList();
        }
        var rows = await DesignRows(db.Set<Design>().Where(d => d.SellerId == sellerId && d.Status != DesignStatus.Removed)).ToListAsync(ct);
        return rows.OrderByDescending(r => r.CreatedAt).Select(r => new StoreDesignDto(ToCard(r), r.Status.ToString())).ToList();
    }
}
