using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 8: the printable catalogue (classic tee, oversized tee, hoodie) with colours, sizes + size chart, product
/// details and print areas. A product that already exists is left as it is (the admin may have changed it).
/// </summary>
public sealed class CatalogueSeeder(AppDbContext db) : ISeeder
{
    private sealed record ColorRow(string En, string Ar, string Hex);
    private sealed record SizeRow(string Size, bool Big, decimal Chest, decimal Length, decimal Sleeve);
    private sealed record DetailRow(string LabelEn, string LabelAr, string ValueEn, string ValueAr);
    private sealed record AreaRow(PrintSide Side, decimal X, decimal Y, decimal W, decimal H);
    private sealed record ProductRow(string Key, string En, string Ar, GarmentKind Kind, string DescriptionEn, string DescriptionAr,
        decimal BasePrice, decimal FactoryCost, decimal BackPrintExtra, decimal BigSizeExtra, decimal MaxPrice, bool HasBackPrint,
        string? Model3d, List<ColorRow> Colors, List<SizeRow> Sizes, List<DetailRow> Details, List<AreaRow> PrintAreas);

    public int Order => 8;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var existing = await db.Set<CatalogProduct>().IgnoreQueryFilters().Select(p => p.Key).ToListAsync(ct);
        var added = 0;
        foreach (var row in SeedFile.Read<List<ProductRow>>("Reference/catalogue.json").Where(r => !existing.Contains(r.Key)))
        {
            var product = ToProduct(row);
            db.Set<CatalogProduct>().Add(product);
            await db.SaveChangesAsync(ct);
            AddParts(product.Id, row);
            await db.SaveChangesAsync(ct);
            added++;
        }
        return added;
    }

    private static CatalogProduct ToProduct(ProductRow r) => new()
    {
        Key = r.Key, NameEn = r.En, NameAr = r.Ar, Kind = r.Kind, DescriptionEn = r.DescriptionEn, DescriptionAr = r.DescriptionAr,
        BasePrice = r.BasePrice, FactoryCost = r.FactoryCost, BackPrintExtra = r.BackPrintExtra, BigSizeExtra = r.BigSizeExtra,
        MaxPrice = r.MaxPrice, HasBackPrint = r.HasBackPrint, Model3dUrl = r.Model3d, Status = CatalogProductStatus.Active,
    };

    private void AddParts(int productId, ProductRow r)
    {
        db.Set<CatalogColor>().AddRange(r.Colors.Select((c, i) =>
            new CatalogColor { CatalogProductId = productId, NameEn = c.En, NameAr = c.Ar, Hex = c.Hex, SortOrder = i + 1 }));
        db.Set<CatalogSize>().AddRange(r.Sizes.Select((s, i) => new CatalogSize
        {
            CatalogProductId = productId, Size = s.Size, IsBigSize = s.Big, ChestCm = s.Chest, LengthCm = s.Length, SleeveCm = s.Sleeve, SortOrder = i + 1,
        }));
        db.Set<CatalogDetail>().AddRange(r.Details.Select((d, i) => new CatalogDetail
        {
            CatalogProductId = productId, LabelEn = d.LabelEn, LabelAr = d.LabelAr, ValueEn = d.ValueEn, ValueAr = d.ValueAr, SortOrder = i + 1,
        }));
        db.Set<CatalogPrintArea>().AddRange(r.PrintAreas.Select(a =>
            new CatalogPrintArea { CatalogProductId = productId, Side = a.Side, X = a.X, Y = a.Y, Width = a.W, Height = a.H }));
    }
}
