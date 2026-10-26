using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Agents;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Admin;

/// <summary>Agents, keys, tasks and runs; the cards carry 30-day runs / errors and this month's cost.</summary>
public sealed class AgentRepository(AppDbContext db, UserManager<User> users) : IAgentRepository
{
    public async Task<IReadOnlyList<AgentDto>> ListAsync(DateTime now, CancellationToken ct) =>
        await Cards(db.Set<Agent>().OrderBy(a => a.PositionId == null).ThenBy(a => a.Name), now).ToListAsync(ct);

    public Task<AgentDto?> CardAsync(long id, DateTime now, CancellationToken ct) =>
        Cards(db.Set<Agent>().Where(a => a.Id == id), now).FirstOrDefaultAsync(ct);

    public async Task<AgentDetailDto?> DetailAsync(long id, DateTime now, CancellationToken ct)
    {
        var card = await CardAsync(id, now, ct);
        if (card is null)
        {
            return null;
        }
        var keys = (await db.Set<ApiKey>().AsNoTracking().Where(k => k.AgentId == id).OrderByDescending(k => k.CreatedAt).ToListAsync(ct))
            .Select(k => new ApiKeyDto(k.Id, k.Name, k.Prefix, k.RateLimitPerMinute, k.CreatedAt, k.ExpiresAt, k.RevokedAt, k.LastUsedAt)).ToList();
        var runs = await db.Set<AgentRun>().AsNoTracking().Where(r => r.AgentId == id).OrderByDescending(r => r.StartedAt).Take(20)
            .Select(r => new AgentRunDto(r.Id, r.TaskId, r.StartedAt, r.FinishedAt, r.Result.ToString(), r.Summary, r.InputTokens, r.OutputTokens, r.CostUsd))
            .ToListAsync(ct);
        return new AgentDetailDto(card, keys, await TasksAsync(id, ct), runs);
    }

    public Task<Agent?> GetAsync(long id, CancellationToken ct) => db.Set<Agent>().FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<bool> NameTakenAsync(string name, long? exceptId, CancellationToken ct) =>
        db.Set<Agent>().AnyAsync(a => a.Name == name && a.Id != exceptId, ct);

    public Task<bool> PositionExistsAsync(long positionId, CancellationToken ct) =>
        db.Set<Domain.Entities.Ops.OrgPosition>().AnyAsync(p => p.Id == positionId, ct);

    public Task<bool> PositionTakenAsync(long positionId, long? exceptAgentId, CancellationToken ct) =>
        db.Set<Agent>().AnyAsync(a => a.PositionId == positionId && a.Id != exceptAgentId, ct);

    public async Task<Guid> CreateServiceUserAsync(string agentName, CancellationToken ct)
    {
        var handle = new string(agentName.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).Take(20).ToArray());
        var login = $"agent.{(handle.Length > 0 ? handle : "ai")}.{Guid.NewGuid():N}"[..40];
        var user = new User
        {
            FullName = $"AI agent {agentName}", UserName = login, Email = login + "@agents.superapp.local", EmailConfirmed = true,
            AccountType = AccountType.AiAgentServiceAccount, Status = UserStatus.Active, Language = "en",
        };
        var result = await users.CreateAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Could not create the agent's service account: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
        return user.Id;
    }

    public async Task SetServiceUserActiveAsync(Guid userId, bool active, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.Status, active ? UserStatus.Active : UserStatus.Suspended)
            .SetProperty(u => u.SuspendedReason, active ? null : "AI agent deleted"), ct);

    public async Task SetPermissionsAsync(Agent agent, IReadOnlyList<string> codes, CancellationToken ct)
    {
        await db.Set<AgentPermission>().Where(p => p.AgentId == agent.Id).ExecuteDeleteAsync(ct);
        var ids = await db.Set<Permission>().Where(p => codes.Contains(p.Code)).Select(p => p.Id).ToListAsync(ct);
        db.Set<AgentPermission>().AddRange(ids.Select(id => new AgentPermission { AgentId = agent.Id, PermissionId = id, CreatedAt = DateTime.UtcNow }));
        await db.SaveChangesAsync(ct);
    }

    public async Task ReplaceTasksAsync(long agentId, IReadOnlyList<AgentTask> tasks, CancellationToken ct)
    {
        await db.Set<AgentRun>().Where(r => r.AgentId == agentId && r.TaskId != null).ExecuteUpdateAsync(s => s.SetProperty(r => r.TaskId, (long?)null), ct);
        await db.Set<AgentTask>().Where(t => t.AgentId == agentId).ExecuteDeleteAsync(ct);
        db.Set<AgentTask>().AddRange(tasks);
        await db.SaveChangesAsync(ct);
    }

    public void Add(Agent agent) => db.Set<Agent>().Add(agent);

    public void Add(ApiKey key) => db.Set<ApiKey>().Add(key);

    public void Add(AgentRun run) => db.Set<AgentRun>().Add(run);

    public Task<ApiKey?> KeyAsync(long agentId, long keyId, CancellationToken ct) =>
        db.Set<ApiKey>().FirstOrDefaultAsync(k => k.Id == keyId && k.AgentId == agentId, ct);

    public Task<bool> PrefixTakenAsync(string prefix, CancellationToken ct) => db.Set<ApiKey>().AnyAsync(k => k.Prefix == prefix, ct);

    public Task<int> RevokeKeysAsync(long agentId, DateTime now, CancellationToken ct) =>
        db.Set<ApiKey>().Where(k => k.AgentId == agentId && k.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(k => k.RevokedAt, now), ct);

    public Task<int> SetAllStatusAsync(AgentStatus from, AgentStatus to, string? reason, CancellationToken ct) =>
        db.Set<Agent>().Where(a => a.Status == from).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, to).SetProperty(a => a.PausedReason, reason), ct);

    public async Task<IReadOnlyList<AgentTaskDto>> TasksAsync(long agentId, CancellationToken ct) =>
        await db.Set<AgentTask>().AsNoTracking().Where(t => t.AgentId == agentId).OrderBy(t => t.Id)
            .Select(t => new AgentTaskDto(t.Id, t.Title, t.Description, t.Trigger.ToString(), t.EventType, t.Cron, t.ExpectedOutput, t.IsEnabled, t.LastRunAt))
            .ToListAsync(ct);

    public Task<AgentRun?> RunAsync(long agentId, long runId, CancellationToken ct) =>
        db.Set<AgentRun>().FirstOrDefaultAsync(r => r.Id == runId && r.AgentId == agentId, ct);

    public Task<bool> TaskBelongsAsync(long agentId, long taskId, CancellationToken ct) =>
        db.Set<AgentTask>().AnyAsync(t => t.Id == taskId && t.AgentId == agentId, ct);

    public Task<decimal> CostThisMonthAsync(long agentId, DateTime now, CancellationToken ct)
    {
        var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return db.Set<AgentRun>().Where(r => r.AgentId == agentId && r.StartedAt >= month).SumAsync(r => r.CostUsd, ct);
    }

    private IQueryable<AgentDto> Cards(IQueryable<Agent> agents, DateTime now)
    {
        var since = now.AddDays(-30);
        var month = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return agents.AsNoTracking().Select(a => new AgentDto(
            a.Id, a.Name, a.TitleEn, a.TitleAr, a.Description, a.Model, a.Platform.ToString(), a.Status.ToString(), a.PausedReason,
            a.MoneyLimit, a.MonthlyBudget, a.Permissions.Select(p => p.Permission.Code).OrderBy(c => c).ToList(), a.PositionId, a.ServiceUserId,
            a.ApiKeys.Count(k => k.RevokedAt == null && (k.ExpiresAt == null || k.ExpiresAt > now)),
            db.Set<AgentRun>().Count(r => r.AgentId == a.Id && r.StartedAt >= since),
            db.Set<AgentRun>().Count(r => r.AgentId == a.Id && r.StartedAt >= since && r.Result == AgentRunResult.Failed),
            db.Set<AgentRun>().Where(r => r.AgentId == a.Id && r.StartedAt >= month).Sum(r => (decimal?)r.CostUsd) ?? 0,
            db.Set<AgentRun>().Where(r => r.AgentId == a.Id).Max(r => (DateTime?)r.StartedAt)));
    }
}
