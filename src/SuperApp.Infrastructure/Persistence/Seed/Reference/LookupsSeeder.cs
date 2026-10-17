using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>Seed 7: pitch facilities, cancellation policies, shop categories, design categories and seller levels.</summary>
public sealed class LookupsSeeder(AppDbContext db) : ISeeder
{
    private sealed record NamedRow(string Key, string En, string Ar, string? Icon);
    private sealed record PolicyRow(string Key, string En, string Ar, short FullHours, short HalfHours, string SummaryEn, string SummaryAr);
    private sealed record LevelRow(string Key, string En, string Ar, byte Rank, int Sales, decimal Rating, int? Designs, int? Codes,
        decimal BaseDiscount, decimal Commission, string Review, byte Boost, decimal SampleDiscount, byte FreeSamples,
        decimal FantasyDiscount, bool Bulk, bool TeamKits, bool EarlyAccess, string Analytics, string Calendar, string Support);
    private sealed record LookupFile(List<NamedRow> Facilities, List<PolicyRow> CancellationPolicies, List<NamedRow> ProductCategories,
        List<NamedRow> DesignCategories, List<LevelRow> SellerLevels);

    public int Order => 7;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var file = SeedFile.Read<LookupFile>("Reference/lookups.json");
        var added = await db.Set<Facility>().AddMissingAsync(
            file.Facilities.Select((f, i) => new Facility { Key = f.Key, NameEn = f.En, NameAr = f.Ar, Icon = f.Icon ?? "check", SortOrder = i + 1 }), f => f.Key, ct);
        added += await db.Set<CancellationPolicy>().AddMissingAsync(file.CancellationPolicies.Select((p, i) => new CancellationPolicy
        {
            Key = p.Key, NameEn = p.En, NameAr = p.Ar, FullRefundHours = p.FullHours, HalfRefundHours = p.HalfHours,
            SummaryEn = p.SummaryEn, SummaryAr = p.SummaryAr, SortOrder = i + 1,
        }), p => p.Key, ct);
        added += await db.Set<ProductCategory>().AddMissingAsync(
            file.ProductCategories.Select((c, i) => new ProductCategory { Key = c.Key, NameEn = c.En, NameAr = c.Ar, Icon = c.Icon ?? "tag", SortOrder = i + 1 }), c => c.Key, ct);
        added += await db.Set<DesignCategory>().AddMissingAsync(
            file.DesignCategories.Select((c, i) => new DesignCategory { Key = c.Key, NameEn = c.En, NameAr = c.Ar, SortOrder = i + 1 }), c => c.Key, ct);
        added += await db.Set<SellerLevel>().AddMissingAsync(file.SellerLevels.Select(ToLevel), l => l.Key, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }

    private static SellerLevel ToLevel(LevelRow l) => new()
    {
        Key = l.Key, NameEn = l.En, NameAr = l.Ar, Rank = l.Rank, SortOrder = l.Rank,
        SalesNeeded = l.Sales, MinRating = l.Rating, ActiveDesigns = l.Designs, ActiveCodes = l.Codes,
        BaseDiscount = l.BaseDiscount, StoreCommission = l.Commission, SearchBoost = l.Boost,
        SampleDiscount = l.SampleDiscount, FreeSamplesPerMonth = l.FreeSamples, FantasyDiscount = l.FantasyDiscount,
        BulkUpload = l.Bulk, TeamKits = l.TeamKits, EarlyAccess = l.EarlyAccess,
        ReviewLane = l.Review switch { "priority" => ReviewLane.Priority, "instant" => ReviewLane.InstantWithSpotCheck, _ => ReviewLane.Normal },
        AnalyticsTier = l.Analytics switch { "advanced" => AnalyticsTier.Advanced, "export" => AnalyticsTier.AdvancedWithExport, _ => AnalyticsTier.Basic },
        SupportTier = l.Support switch { "priority" => SupportTier.Priority, "manager" => SupportTier.AccountManager, _ => SupportTier.Normal },
        CalendarTier = l.Calendar switch { "weather" => 1, "trends" => 2, "early" => 3, _ => 0 },
    };
}
