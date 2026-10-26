using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Agents;
using SuperApp.Application.Features.CeoSystem;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Admin;

/// <summary>Finds agent keys by prefix for the API-key sign-in; "last used" is written at most once a minute.</summary>
public sealed class AgentKeyStore(AppDbContext db) : IAgentKeyStore
{
    public async Task<AgentKeyIdentity?> FindAsync(string prefix, CancellationToken ct)
    {
        var row = await db.Set<ApiKey>().AsNoTracking().Where(k => k.Prefix == prefix && k.AgentId != null && !k.Agent!.IsDeleted)
            .Select(k => new
            {
                k.Id, AgentId = k.AgentId!.Value, k.Agent!.ServiceUserId, k.Agent.Name, k.Prefix, k.KeyHash, Status = k.Agent.Status,
                k.ExpiresAt, k.RevokedAt, k.LastUsedAt, k.RateLimitPerMinute, Permissions = k.Agent.Permissions.Select(p => p.Permission.Code).ToList(),
            }).FirstOrDefaultAsync(ct);
        return row is null ? null : new AgentKeyIdentity(row.Id, row.AgentId, row.ServiceUserId, row.Name, row.Prefix, row.KeyHash, row.Status.ToString(),
            row.ExpiresAt, row.RevokedAt, row.LastUsedAt, row.RateLimitPerMinute, row.Permissions);
    }

    public Task TouchAsync(long keyId, DateTime now, CancellationToken ct) =>
        db.Set<ApiKey>().Where(k => k.Id == keyId).ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now), ct);
}

/// <summary>ops.FeatureFlags: the kill switches and API group switches.</summary>
public sealed class SwitchRepository(AppDbContext db) : ISwitchRepository
{
    public async Task<IReadOnlyList<SwitchDto>> ListAsync(FeatureFlagKind? kind, CancellationToken ct) =>
        await Project(db.Set<FeatureFlag>().Where(f => kind == null || f.Kind == kind).OrderBy(f => f.Id)).ToListAsync(ct);

    public Task<SwitchDto?> GetAsync(string key, CancellationToken ct) => Project(db.Set<FeatureFlag>().Where(f => f.Key == key)).FirstOrDefaultAsync(ct);

    public async Task<bool> IsOnAsync(string key, CancellationToken ct) =>
        await db.Set<FeatureFlag>().Where(f => f.Key == key).Select(f => (bool?)f.IsEnabled).FirstOrDefaultAsync(ct) ?? true;

    public async Task<bool> SetAsync(string key, bool enabled, string? reason, Guid? byUserId, CancellationToken ct) =>
        await db.Set<FeatureFlag>().Where(f => f.Key == key).ExecuteUpdateAsync(s => s
            .SetProperty(f => f.IsEnabled, enabled).SetProperty(f => f.Reason, reason)
            .SetProperty(f => f.ChangedById, byUserId).SetProperty(f => f.UpdatedAt, DateTime.UtcNow), ct) > 0;

    private IQueryable<SwitchDto> Project(IQueryable<FeatureFlag> flags) => flags.AsNoTracking().Select(f => new SwitchDto(
        f.Key, f.Kind.ToString(), f.IsEnabled, f.Reason,
        db.Users.Where(u => u.Id == f.ChangedById).Select(u => u.FullName).FirstOrDefault(), f.UpdatedAt));
}

/// <summary>Departments and positions; a position's holder is a person or the agent sitting in it.</summary>
public sealed class OrgRepository(AppDbContext db) : IOrgRepository
{
    public async Task<OrgChartDto> ChartAsync(CancellationToken ct)
    {
        var departments = await db.Set<Department>().AsNoTracking().OrderBy(d => d.SortOrder)
            .Select(d => new DepartmentDto(d.Id, d.Key, d.NameEn, d.NameAr, d.Color, d.SortOrder)).ToListAsync(ct);
        var rows = await db.Set<OrgPosition>().AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .Select(p => new
            {
                p.Id, p.DepartmentId, p.ReportsToId, p.TitleEn, p.TitleAr, p.HolderUserId, p.DutiesJson, p.SortOrder,
                Person = p.HolderUser == null ? null : p.HolderUser.FullName,
                Agent = db.Set<Agent>().Where(a => a.PositionId == p.Id).Select(a => new { a.Id, a.Name, a.Status }).FirstOrDefault(),
            }).ToListAsync(ct);
        var positions = rows.Select(p => new PositionDto(p.Id, p.DepartmentId, p.ReportsToId, p.TitleEn, p.TitleAr,
            p.Agent is not null ? new HolderDto("agent", null, p.Agent.Name, p.Agent.Id, p.Agent.Status.ToString())
            : p.HolderUserId is not null ? new HolderDto("human", p.HolderUserId, p.Person, null, null) : new HolderDto("vacant", null, null, null, null),
            Duties(p.DutiesJson), p.SortOrder)).ToList();
        return new OrgChartDto(departments, positions);
    }

    public Task<OrgPosition?> PositionAsync(long id, CancellationToken ct) => db.Set<OrgPosition>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> DepartmentExistsAsync(int id, CancellationToken ct) => db.Set<Department>().AnyAsync(d => d.Id == id, ct);

    public Task<bool> PersonExistsAsync(Guid userId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == userId && u.AccountType == AccountType.Person, ct);

    public Task<bool> HasReportsAsync(long id, CancellationToken ct) => db.Set<OrgPosition>().AnyAsync(p => p.ReportsToId == id, ct);

    public async Task<bool> WouldLoopAsync(long id, long newParentId, CancellationToken ct)
    {
        var parents = await db.Set<OrgPosition>().AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.ReportsToId, ct);
        long? current = newParentId;
        for (var guard = 0; current is { } c && guard < 1000; guard++)
        {
            if (c == id)
            {
                return true;
            }
            current = parents.GetValueOrDefault(c);
        }
        return false;
    }

    public Task DetachAgentsAsync(long positionId, CancellationToken ct) =>
        db.Set<Agent>().IgnoreQueryFilters().Where(a => a.PositionId == positionId).ExecuteUpdateAsync(s => s.SetProperty(a => a.PositionId, (long?)null), ct);

    public void Add(OrgPosition position) => db.Set<OrgPosition>().Add(position);

    public void Remove(OrgPosition position) => db.Set<OrgPosition>().Remove(position);

    private static List<string> Duties(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}

/// <summary>The integrations registry with today's usage (and 30 days on the detail page).</summary>
public sealed class IntegrationRepository(AppDbContext db, Application.Features.CeoSystem.ISecretPresence secrets) : IIntegrationRepository
{
    public async Task<IReadOnlyList<IntegrationDto>> ListAsync(DateOnly today, CancellationToken ct)
    {
        var rows = await db.Set<Integration>().AsNoTracking().OrderBy(i => i.Category).ThenByDescending(i => i.IsPrimary).ThenBy(i => i.Name).ToListAsync(ct);
        var usage = await db.Set<IntegrationUsage>().AsNoTracking().Where(u => u.Day == today).ToDictionaryAsync(u => u.IntegrationId, ct);
        return rows.Select(i => ToDto(i, usage.GetValueOrDefault(i.Id), null)).ToList();
    }

    public async Task<IntegrationDto?> DetailAsync(string key, DateOnly today, CancellationToken ct)
    {
        var integration = await db.Set<Integration>().AsNoTracking().FirstOrDefaultAsync(i => i.Key == key, ct);
        if (integration is null)
        {
            return null;
        }
        var from = today.AddDays(-29);
        var days = await db.Set<IntegrationUsage>().AsNoTracking().Where(u => u.IntegrationId == integration.Id && u.Day >= from)
            .OrderBy(u => u.Day).ToListAsync(ct);
        return ToDto(integration, days.FirstOrDefault(d => d.Day == today), days.Select(Day).ToList());
    }

    public Task<Integration?> GetAsync(string key, CancellationToken ct) => db.Set<Integration>().FirstOrDefaultAsync(i => i.Key == key, ct);

    public Task ClearPrimaryAsync(IntegrationCategory category, int exceptId, CancellationToken ct) =>
        db.Set<Integration>().Where(i => i.Category == category && i.Id != exceptId && i.IsPrimary)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsPrimary, false), ct);

    private IntegrationDto ToDto(Integration i, IntegrationUsage? today, IReadOnlyList<UsageDayDto>? usage) => new(
        i.Key, i.Category.ToString(), i.Name, i.Mode.ToString(), i.IsEnabled, i.IsPrimary, i.Status.ToString(), i.SecretName, secrets.IsSet(i.SecretName),
        i.LastCallAt, i.LastErrorAt, i.LastError, i.DailyQuota, today is null ? null : Day(today),
        string.IsNullOrWhiteSpace(i.ConfigJson) ? null : JsonDocument.Parse(i.ConfigJson).RootElement.Clone(), usage);

    private static UsageDayDto Day(IntegrationUsage u) => new(u.Day, u.Calls, u.Errors, u.QuotaUsed, u.QuotaLimit);
}

/// <summary>A secret is "set" when its environment variable (or the matching configuration key) has a value.</summary>
public sealed class EnvSecretPresence(Microsoft.Extensions.Configuration.IConfiguration configuration) : Application.Features.CeoSystem.ISecretPresence
{
    public bool IsSet(string? environmentVariable) =>
        !string.IsNullOrWhiteSpace(environmentVariable)
        && (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(environmentVariable))
            || !string.IsNullOrWhiteSpace(configuration[environmentVariable.Replace("__", ":", StringComparison.Ordinal)]));
}
