using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Geo;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>Seed 1: the 27 governorates and the main areas (Cairo, Giza and Alexandria first).</summary>
public sealed class GeoSeeder(AppDbContext db) : ISeeder
{
    private sealed record GovernorateRow(string Code, string En, string Ar);
    private sealed record AreaRow(string Governorate, string En, string Ar, decimal Lat, decimal Lng);

    public int Order => 1;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var governorates = SeedFile.Read<List<GovernorateRow>>("Reference/governorates.json")
            .Select((g, i) => new Governorate { Code = g.Code, NameEn = g.En, NameAr = g.Ar, SortOrder = i + 1 });
        var added = await db.Set<Governorate>().AddMissingAsync(governorates, g => g.Code, ct);
        await db.SaveChangesAsync(ct);

        var ids = await db.Set<Governorate>().ToDictionaryAsync(g => g.Code, g => g.Id, ct);
        var areas = SeedFile.Read<List<AreaRow>>("Reference/areas.json")
            .Select((a, i) => new Area { GovernorateId = ids[a.Governorate], NameEn = a.En, NameAr = a.Ar, Lat = a.Lat, Lng = a.Lng, SortOrder = i + 1 });
        added += await db.Set<Area>().AddMissingAsync(areas, a => (a.GovernorateId, a.NameEn), ct);
        await db.SaveChangesAsync(ct);
        return added;
    }
}
