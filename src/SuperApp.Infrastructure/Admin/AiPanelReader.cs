using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Agents;
using SuperApp.Application.Features.CeoSystem;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;
using SuperApp.Infrastructure.Persistence.Seed;

namespace SuperApp.Infrastructure.Admin;

/// <summary>The AI panel numbers (runs, cost, errors per agent) and agent traffic per API group.</summary>
public sealed class AiPanelReader(AppDbContext db, IAgentRepository agents) : IAiPanelReader
{
    public const string AgentCallAction = "agent.call";

    public async Task<AiPanelDto> PanelAsync(bool agentsSwitchOn, DateTime now, CancellationToken ct)
    {
        var cards = await agents.ListAsync(now, ct);
        var today = now.Date;
        var since = now.AddDays(-30);
        var runsToday = await db.Set<AgentRun>().CountAsync(r => r.StartedAt >= today, ct);
        var errors = await db.Set<AgentRun>().AsNoTracking().Where(r => r.Result == AgentRunResult.Failed && r.StartedAt >= since)
            .OrderByDescending(r => r.StartedAt).Take(10)
            .Select(r => new FailedRunDto(r.Id, r.AgentId, r.Agent.Name, r.StartedAt, r.Summary)).ToListAsync(ct);
        return new AiPanelDto(agentsSwitchOn, cards.Count,
            cards.Count(c => c.Status == nameof(AgentStatus.Active)), cards.Count(c => c.Status == nameof(AgentStatus.Paused)),
            cards.Count(c => c.Status == nameof(AgentStatus.EmergencyStopped)), cards.Sum(c => c.ActiveKeys),
            runsToday, cards.Sum(c => c.Runs30d), cards.Sum(c => c.Errors30d), cards.Sum(c => c.CostThisMonthUsd), cards.Sum(c => c.MonthlyBudget),
            cards.OrderByDescending(c => c.CostThisMonthUsd).ThenBy(c => c.Name)
                .Select(c => new AgentCostDto(c.Id, c.Name, c.Status, c.CostThisMonthUsd, c.MonthlyBudget, c.Runs30d, c.Errors30d)).ToList(),
            errors);
    }

    public async Task<IReadOnlyDictionary<string, int>> AgentCallsByGroupAsync(DateTime since, CancellationToken ct) =>
        await db.Set<AuditLog>().Where(a => a.Action == AgentCallAction && a.At >= since && a.EntityId != null)
            .GroupBy(a => a.EntityId!).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
}

/// <summary>
/// The audit logger for Phase 7: the same line as before, plus the agent behind an API key (<c>ActorAgentId</c>, system
/// ApiOrAgent) so every agent action can be traced to its agent.
/// </summary>
public sealed class AgentAwareAuditLogger(AppDbContext db, ICurrentUser currentUser, ICurrentAgent currentAgent, IRequestInfo request, IClock clock) : IAuditLogger
{
    public async Task LogAsync(AuditSystem system, string action, string summary, string? entityType = null, string? entityId = null,
        object? data = null, CancellationToken ct = default)
    {
        var agent = currentAgent.Caller;
        db.Set<AuditLog>().Add(new AuditLog
        {
            At = clock.UtcNow,
            ActorUserId = currentUser.UserId,
            ActorAgentId = agent?.AgentId,
            System = agent is null ? system : AuditSystem.ApiOrAgent,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary.Length > 400 ? summary[..400] : summary,
            DataJson = data is null ? null : SeedFile.ToJson(data),
            Ip = request.IpAddress,
            CorrelationId = request.CorrelationId,
        });
        // Events n8n follows are queued in the same save as their activity line (sent by WebhookDispatcher)
        if (Application.Features.Webhooks.WebhookEvents.FromAudit(action) is { } evt)
        {
            db.Set<Domain.Entities.Ops.OutboxEvent>().Add(new Domain.Entities.Ops.OutboxEvent
            {
                Type = evt.Type, OccurredAt = clock.UtcNow,
                PayloadJson = SeedFile.ToJson(new { entityType, entityId, summary, by = evt.By, data }),
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
