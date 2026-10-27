using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Demo 14: the stores (official store, a pitch partner's shop, sellers' stores) and the 35 products with images,
/// specs and one variant per size and colour (the product's stock is shared out between its variants).
/// </summary>
public sealed class DemoShopSeeder(AppDbContext db) : ISeeder
{
    private sealed record StoreRow(string Handle, string Name, string? Owner, string Type, string? Tagline, string? About, string? Cover);
    private sealed record SpecRow(string Label, string Value);
    private sealed record VariantRow(string Size, string Color, string? Hex, string Sku, int Stock);
    private sealed record ProductRow(string Slug, string Store, string Category, string Name, string? Brand, string? Description, decimal Price,
        decimal? CompareAt, int LowStockAt, decimal Rating, List<string> Images, List<SpecRow> Specs, List<VariantRow> Variants);
    private sealed record ShopFile(List<StoreRow> Stores, List<ProductRow> Products);

    public int Order => 14;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var file = SeedFile.Read<ShopFile>("Demo/shop.json");
        var users = await DemoRefs.UsersAsync(db, ct);
        var files = await DemoRefs.FilesAsync(db, ct);
        var stores = file.Stores.Select(s => new Store
        {
            Handle = s.Handle, Name = s.Name, OwnerUserId = s.Owner is null ? null : users[s.Owner], Type = DemoRefs.Parse<StoreType>(s.Type),
            Tagline = s.Tagline, About = s.About, CoverFileId = s.Cover is null ? null : files[s.Cover], AccentColor = "#0B6E4F", Status = StoreStatus.Active,
        });
        var added = await db.Set<Store>().AddMissingAsync(stores, s => s.Handle, ct);
        await db.SaveChangesAsync(ct);

        var existing = await db.Set<Product>().IgnoreQueryFilters().Select(p => p.Slug).ToListAsync(ct);
        foreach (var row in file.Products.Where(p => !existing.Contains(p.Slug)))
        {
            await AddProductAsync(row, files, ct);
            added++;
        }
        return added;
    }

    private async Task AddProductAsync(ProductRow row, Dictionary<string, long> files, CancellationToken ct)
    {
        var product = new Product
        {
            StoreId = await db.Set<Store>().Where(s => s.Handle == row.Store).Select(s => s.Id).SingleAsync(ct),
            CategoryId = await db.Set<ProductCategory>().Where(c => c.Key == row.Category).Select(c => c.Id).SingleAsync(ct),
            Slug = row.Slug, Name = row.Name, Brand = row.Brand, Description = row.Description, Price = row.Price, CompareAtPrice = row.CompareAt,
            LowStockAt = row.LowStockAt, Rating = row.Rating, Status = ProductStatus.Published, ApprovedAt = DateTime.UtcNow,
        };
        db.Set<Product>().Add(product);
        await db.SaveChangesAsync(ct);

        db.Set<ProductImage>().AddRange(row.Images.Distinct().Select((key, i) => new ProductImage { ProductId = product.Id, FileId = files[key], SortOrder = i + 1 }));
        db.Set<ProductSpec>().AddRange(row.Specs.Select((s, i) => new ProductSpec { ProductId = product.Id, Label = s.Label, Value = s.Value, SortOrder = i + 1 }));
        db.Set<ProductVariant>().AddRange(row.Variants.Select(v => new ProductVariant
        {
            ProductId = product.Id, Size = v.Size, ColorName = v.Color, ColorHex = v.Hex, Sku = v.Sku, Stock = v.Stock,
        }));
        await db.SaveChangesAsync(ct);
    }
}
