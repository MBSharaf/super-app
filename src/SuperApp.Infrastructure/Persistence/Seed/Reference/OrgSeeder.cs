using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 4: the 8 departments and the human positions of the org chart (CEO, admin manager, CFO, legal counsel).
/// The CEO position is held by the CEO account; the AI agents' positions come with the demo data.
/// </summary>
public sealed class OrgSeeder(AppDbContext db) : ISeeder
{
    internal sealed record DepartmentRow(string Key, string En, string Ar, string Color);
    internal sealed record PositionRow(string Key, string? Dept, string? ReportsTo, string En, string Ar, string Holder, string? Name,
        decimal? Limit, List<string> Can, List<string> Duties, int Sort);
    internal sealed record OrgFile(List<DepartmentRow> Departments, List<PositionRow> Positions);

    internal static OrgFile Load() => SeedFile.Read<OrgFile>("Reference/org.json");

    public int Order => 4;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var org = Load();
        var departments = org.Departments.Select((d, i) => new Department { Key = d.Key, NameEn = d.En, NameAr = d.Ar, Color = d.Color, SortOrder = i + 1 });
        var added = await db.Set<Department>().AddMissingAsync(departments, d => d.Key, ct);
        await db.SaveChangesAsync(ct);

        added += await AddPositionsAsync(db, org, p => p.Holder == "human", ct);
        await AssignCeoAsync(org, ct);
        return added;
    }

    /// <summary>
    /// Adds the chosen positions in chart order, so each one can point to the position it reports to
    /// (matched by title, the natural key). Shared with the AI agents seed.
    /// </summary>
    internal static async Task<int> AddPositionsAsync(AppDbContext db, OrgFile org, Func<PositionRow, bool> which, CancellationToken ct)
    {
        var departments = await db.Set<Department>().ToDictionaryAsync(d => d.Key, d => d.Id, ct);
        var titleOf = org.Positions.ToDictionary(p => p.Key, p => p.En);
        var added = 0;
        foreach (var row in org.Positions.Where(which).OrderBy(r => r.Sort))
        {
            var existing = await db.Set<OrgPosition>().ToDictionaryAsync(p => p.TitleEn, p => p.Id, ct);
            if (existing.ContainsKey(row.En))
            {
                continue;
            }
            db.Set<OrgPosition>().Add(new OrgPosition
            {
                DepartmentId = row.Dept is null ? null : departments[row.Dept],
                ReportsToId = row.ReportsTo is not null && existing.TryGetValue(titleOf[row.ReportsTo], out var parent) ? parent : null,
                TitleEn = row.En,
                TitleAr = row.Ar,
                DutiesJson = SeedFile.ToJson(row.Duties),
                SortOrder = row.Sort,
            });
            await db.SaveChangesAsync(ct);
            added++;
        }
        return added;
    }

    private async Task AssignCeoAsync(OrgFile org, CancellationToken ct)
    {
        var title = org.Positions.First(p => p.Key == "ceo").En;
        var position = await db.Set<OrgPosition>().FirstAsync(p => p.TitleEn == title, ct);
        var ceoId = await db.UserRoles.Where(ur => ur.Role.Name == SystemRoles.Ceo).Select(ur => (Guid?)ur.UserId).FirstOrDefaultAsync(ct);
        if (ceoId is not null && position.HolderUserId is null)
        {
            position.HolderUserId = ceoId;
            await db.SaveChangesAsync(ct);
        }
    }
}
