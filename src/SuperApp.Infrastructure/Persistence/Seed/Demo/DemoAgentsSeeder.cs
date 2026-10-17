using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence.Seed.Reference;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Demo 19: the 18 AI agents of the org chart — their positions under the 8 departments, service accounts,
/// permissions and money limits. All start <b>Paused</b> with no API key: the CEO starts each one from the AI panel.
/// </summary>
public sealed class DemoAgentsSeeder(AppDbContext db) : ISeeder
{
    private const string DefaultModel = "claude-sonnet-5-5";

    private sealed record AgentRow(string Position, string User, string Name, string TitleEn, List<string> Duties, decimal MoneyLimit,
        decimal MonthlyBudget, List<string> Permissions);

    public int Order => 19;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var org = OrgSeeder.Load();
        var added = await OrgSeeder.AddPositionsAsync(db, org, p => p.Holder == "ai", ct);
        var titles = org.Positions.ToDictionary(p => p.Key, p => (p.En, p.Ar));
        var users = await DemoRefs.UsersAsync(db, ct);
        var existing = await db.Set<Agent>().IgnoreQueryFilters().Select(a => a.Name).ToListAsync(ct);
        foreach (var row in SeedFile.Read<List<AgentRow>>("Demo/agents.json").Where(a => !existing.Contains(a.Name)))
        {
            await AddAgentAsync(row, titles[row.Position], users[row.User], ct);
            added++;
        }
        return added;
    }

    private async Task AddAgentAsync(AgentRow row, (string En, string Ar) title, Guid userId, CancellationToken ct)
    {
        var position = await db.Set<OrgPosition>().SingleAsync(p => p.TitleEn == title.En, ct);
        position.HolderUserId = userId;
        var permissions = await db.Set<Permission>().Where(p => row.Permissions.Contains(p.Code)).Select(p => p.Id).ToListAsync(ct);
        db.Set<Agent>().Add(new Agent
        {
            ServiceUserId = userId, PositionId = position.Id, Name = row.Name, TitleEn = title.En, TitleAr = title.Ar,
            Description = string.Join(" · ", row.Duties), Model = DefaultModel, Platform = AgentPlatform.N8n,
            MoneyLimit = row.MoneyLimit, MonthlyBudget = row.MonthlyBudget, Status = AgentStatus.Paused,
            PausedReason = "Demo agent — paused until the CEO starts it and gives it an API key.",
            Permissions = permissions.Select(id => new AgentPermission { PermissionId = id }).ToList(),
        });
        await db.SaveChangesAsync(ct);
    }
}
