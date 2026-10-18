using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Stores;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Stores, their products and profiles. Every query is limited to one store id.</summary>
public sealed partial class StoreRepository(AppDbContext db) : IStoreRepository
{
    public Task<Store?> StoreAsync(long storeId, CancellationToken ct) => db.Set<Store>().FirstOrDefaultAsync(s => s.Id == storeId, ct);

    public Task<Store?> OwnedStoreAsync(Guid ownerUserId, StoreType type, CancellationToken ct) =>
        db.Set<Store>().FirstOrDefaultAsync(s => s.OwnerUserId == ownerUserId && s.Type == type, ct);

    public Task<Store?> OfficialStoreAsync(CancellationToken ct) => db.Set<Store>().FirstOrDefaultAsync(s => s.Type == StoreType.Official, ct);

    public void AddStore(Store store) => db.Set<Store>().Add(store);

    public async Task<string> FreeHandleAsync(string name, CancellationToken ct)
    {
        var handle = Slug(name, 34, "store");
        var taken = await db.Set<Store>().IgnoreQueryFilters().Where(s => s.Handle.StartsWith(handle)).Select(s => s.Handle).ToListAsync(ct);
        return Free(handle, taken);
    }

    public Task<bool> HandleTakenAsync(string handle, long exceptStoreId, CancellationToken ct) =>
        db.Set<Store>().IgnoreQueryFilters().AnyAsync(s => s.Handle == handle && s.Id != exceptStoreId, ct);

    public async Task<StoreProfileDto> ProfileAsync(long storeId, CancellationToken ct)
    {
        var s = await db.Set<Store>().AsNoTracking().Include(x => x.LogoFile).Include(x => x.CoverFile).SingleAsync(x => x.Id == storeId, ct);
        var seller = await db.Set<Seller>().AsNoTracking().Where(x => x.StoreId == storeId).Select(x => new { x.Mode, x.FeaturedDesignId }).FirstOrDefaultAsync(ct);
        return new StoreProfileDto(s.Handle, s.Name, s.Type.ToString(), s.Tagline, s.About, s.AccentColor, s.CoverPositionY,
            ShopQueries.Photo(s.LogoFile?.Container, s.LogoFile?.StorageKey, s.LogoFileId), ShopQueries.Photo(s.CoverFile?.Container, s.CoverFile?.StorageKey, s.CoverFileId),
            s.Status.ToString(), s.BreakUntil, s.BreakNote,
            seller is null ? null : seller.Mode switch { SellerMode.OwnProducts => "store", SellerMode.Both => "both", _ => "pod" }, seller?.FeaturedDesignId);
    }

    public async Task<IReadOnlyList<OwnProductDto>> ProductsAsync(long storeId, CancellationToken ct) =>
        (await Products().Where(p => p.StoreId == storeId).OrderByDescending(p => p.CreatedAt).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<OwnProductDto?> ProductDtoAsync(long storeId, long productId, CancellationToken ct)
    {
        var p = await Products().FirstOrDefaultAsync(x => x.StoreId == storeId && x.Id == productId, ct);
        return p is null ? null : ToDto(p);
    }

    private IQueryable<Product> Products() => db.Set<Product>().AsNoTracking().Include(p => p.Category).Include(p => p.Images).ThenInclude(i => i.File)
        .Include(p => p.Variants).Include(p => p.Specs).AsSplitQuery();

    private static OwnProductDto ToDto(Product p)
    {
        var stock = p.Variants.Sum(v => v.Stock - v.Reserved);
        return new OwnProductDto(p.Id, p.Slug, p.Name, p.Category.Key, p.Brand, p.Description, p.Price, p.CompareAtPrice, p.LowStockAt, p.Status.ToString(),
            p.HiddenByAdmin, p.HiddenReason, stock, stock <= p.LowStockAt,
            p.Images.OrderBy(i => i.SortOrder).Select(i => new OwnImageDto(i.Id, ShopQueries.Photo(i.File.Container, i.File.StorageKey, i.FileId)!, i.SortOrder)).ToList(),
            p.Variants.OrderBy(v => v.Id).Select(v => new OwnVariantDto(v.Id, v.Size, v.ColorName, v.ColorHex, v.Sku, v.Stock, v.Reserved, v.PriceOverride)).ToList(),
            p.Specs.OrderBy(s => s.SortOrder).Select(s => new SpecRequest(s.Label, s.Value)).ToList(), p.CreatedAt);
    }

    public Task<Product?> ProductAsync(long storeId, long productId, CancellationToken ct) =>
        db.Set<Product>().Include(p => p.Variants).Include(p => p.Specs).Include(p => p.Images).AsSplitQuery()
            .FirstOrDefaultAsync(p => p.StoreId == storeId && p.Id == productId, ct);

    public void AddProduct(Product product) => db.Set<Product>().Add(product);

    public Task<int?> CategoryIdAsync(string key, CancellationToken ct) =>
        db.Set<ProductCategory>().Where(c => c.Key == key).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);

    public async Task<string> FreeSlugAsync(string name, CancellationToken ct)
    {
        var slug = Slug(name, 110, "product");
        var taken = await db.Set<Product>().IgnoreQueryFilters().Where(p => p.Slug.StartsWith(slug)).Select(p => p.Slug).ToListAsync(ct);
        return Free(slug, taken);
    }

    public async Task<string> NewSkuAsync(string slug, CancellationToken ct)
    {
        var stem = new string(slug.ToUpperInvariant().Where(char.IsLetterOrDigit).Take(20).ToArray());
        while (true)
        {
            var sku = $"{(stem.Length == 0 ? "SKU" : stem)}-{RandomNumberGenerator.GetInt32(1000, 10000)}";
            if (!await SkuTakenAsync(sku, null, ct) && !db.Set<ProductVariant>().Local.Any(v => v.Sku == sku))
            {
                return sku;
            }
        }
    }

    public Task<bool> SkuTakenAsync(string sku, long? exceptVariantId, CancellationToken ct) =>
        db.Set<ProductVariant>().IgnoreQueryFilters().AnyAsync(v => v.Sku == sku && v.Id != exceptVariantId, ct);

    public async Task<bool> VariantInUseAsync(long variantId, CancellationToken ct) =>
        await db.Set<OrderItem>().AnyAsync(i => i.ProductVariantId == variantId, ct) || await db.Set<CartItem>().AnyAsync(c => c.ProductVariantId == variantId, ct);

    public void RemoveVariant(ProductVariant variant) => db.Set<ProductVariant>().Remove(variant);

    public Task<string> CustomerNameAsync(Guid customerId, CancellationToken ct) =>
        db.Users.IgnoreQueryFilters().Where(u => u.Id == customerId).Select(u => u.FullName).SingleAsync(ct);

    public Task<Seller?> SellerOfStoreAsync(long storeId, CancellationToken ct) => db.Set<Seller>().FirstOrDefaultAsync(s => s.StoreId == storeId, ct);

    public Task<bool> IsLiveDesignOfAsync(long sellerId, long designId, CancellationToken ct) =>
        db.Set<Design>().AnyAsync(d => d.Id == designId && d.SellerId == sellerId && d.Status == DesignStatus.Live, ct);

    public async Task<(Product Product, Guid? OwnerUserId)?> AnyProductAsync(long productId, CancellationToken ct)
    {
        var product = await db.Set<Product>().Include(p => p.Store).FirstOrDefaultAsync(p => p.Id == productId, ct);
        return product is null ? null : (product, product.Store.OwnerUserId);
    }

    /// <summary>Lowercase ASCII letters, digits and dashes (Arabic names fall back to <paramref name="fallback"/>).</summary>
    internal static string Slug(string text, int max, string fallback)
    {
        var sb = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD).ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') { sb.Append(c); }
            else if ((char.IsWhiteSpace(c) || c is '-' or '_' or '&') && sb.Length > 0 && sb[^1] != '-') { sb.Append('-'); }
        }
        var slug = sb.ToString().Trim('-');
        slug = slug.Length > max ? slug[..max].Trim('-') : slug;
        return slug.Length < 3 ? $"{fallback}-{RandomNumberGenerator.GetInt32(1000, 10000).ToString(CultureInfo.InvariantCulture)}" : slug;
    }

    internal static string Free(string slug, IReadOnlyCollection<string> taken)
    {
        if (!taken.Contains(slug))
        {
            return slug;
        }
        for (var n = 2; ; n++)
        {
            var candidate = $"{slug}-{n}";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
