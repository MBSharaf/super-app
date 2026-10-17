using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Lookups;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Read-only queries for the public lookups. Hidden (inactive) rows are left out.</summary>
public sealed class LookupsReader(AppDbContext db) : ILookupsReader
{
    /// <summary>Settings a visitor may read. Commissions, money limits and anything internal stay private.</summary>
    private static readonly string[] PublicKeys =
        ["fees.service", "vat.rate", "delivery.fees", "loyalty.earnPer", "loyalty.pointValue", "loyalty.minRedeem", "fantasy.passPrice", "popups.timing", "content.announcement"];

    public async Task<IReadOnlyList<GovernorateDto>> GovernoratesAsync(CancellationToken ct) =>
        await db.Set<Governorate>().Where(g => g.IsActive).OrderBy(g => g.SortOrder)
            .Select(g => new GovernorateDto(g.Id, g.Code, g.NameEn, g.NameAr)).ToListAsync(ct);

    public async Task<IReadOnlyList<AreaDto>> AreasAsync(int? governorateId, CancellationToken ct) =>
        await db.Set<Area>().Where(a => a.IsActive && (governorateId == null || a.GovernorateId == governorateId)).OrderBy(a => a.SortOrder)
            .Select(a => new AreaDto(a.Id, a.GovernorateId, a.NameEn, a.NameAr, a.Lat, a.Lng)).ToListAsync(ct);

    public async Task<IReadOnlyList<NamedLookupDto>> FacilitiesAsync(CancellationToken ct) =>
        await db.Set<Facility>().Where(f => f.IsActive).OrderBy(f => f.SortOrder)
            .Select(f => new NamedLookupDto(f.Id, f.Key, f.NameEn, f.NameAr, f.Icon)).ToListAsync(ct);

    public async Task<IReadOnlyList<CancellationPolicyDto>> CancellationPoliciesAsync(CancellationToken ct) =>
        await db.Set<CancellationPolicy>().Where(p => p.IsActive).OrderBy(p => p.SortOrder)
            .Select(p => new CancellationPolicyDto(p.Id, p.Key, p.NameEn, p.NameAr, p.FullRefundHours, p.HalfRefundHours, p.SummaryEn, p.SummaryAr)).ToListAsync(ct);

    public async Task<IReadOnlyList<NamedLookupDto>> ProductCategoriesAsync(CancellationToken ct) =>
        await db.Set<ProductCategory>().Where(c => c.IsActive && !c.IsHidden).OrderBy(c => c.SortOrder)
            .Select(c => new NamedLookupDto(c.Id, c.Key, c.NameEn, c.NameAr, c.Icon)).ToListAsync(ct);

    public async Task<IReadOnlyList<NamedLookupDto>> DesignCategoriesAsync(CancellationToken ct) =>
        await db.Set<DesignCategory>().Where(c => c.IsActive).OrderBy(c => c.SortOrder)
            .Select(c => new NamedLookupDto(c.Id, c.Key, c.NameEn, c.NameAr, null)).ToListAsync(ct);

    public async Task<IReadOnlyList<SellerLevelDto>> SellerLevelsAsync(CancellationToken ct)
    {
        var levels = await db.Set<SellerLevel>().Where(l => l.IsActive).OrderBy(l => l.Rank).AsNoTracking().ToListAsync(ct);
        return levels.Select(l => new SellerLevelDto(l.Id, l.Key, l.NameEn, l.NameAr, l.Rank, l.SalesNeeded, l.MinRating, l.ActiveDesigns, l.ActiveCodes,
            l.BaseDiscount, l.StoreCommission, l.ReviewLane.ToString(), l.SearchBoost, l.SampleDiscount, l.FreeSamplesPerMonth, l.FantasyDiscount,
            l.BulkUpload, l.TeamKits, l.EarlyAccess, l.AnalyticsTier.ToString(), l.SupportTier.ToString())).ToList();
    }

    public async Task<IReadOnlyDictionary<string, object?>> PublicSettingsAsync(CancellationToken ct)
    {
        var rows = await db.Set<Setting>().Where(s => PublicKeys.Contains(s.Key)).Select(s => new { s.Key, s.Value }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => (object?)Unwrap(r.Value));
    }

    public async Task<IReadOnlyList<HolidayDto>> HolidaysAsync(int year, CancellationToken ct) =>
        await db.Set<PublicHoliday>().Where(h => h.Date.Year == year).OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Date, h.NameEn, h.NameAr, h.IsEstimated)).ToListAsync(ct);

    /// <summary>Scalar settings are stored as {"value": …}; the site gets the plain value.</summary>
    private static JsonElement Unwrap(string json)
    {
        var element = JsonDocument.Parse(json).RootElement.Clone();
        return element.ValueKind == JsonValueKind.Object && element.EnumerateObject().Count() == 1 && element.TryGetProperty("value", out var value) ? value : element;
    }
}
