using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Agents;

/// <summary>
/// The agent API (API §9.1) for n8n and other automations: who am I, my tasks, and reporting runs with their cost.
/// When this month's cost passes the agent's monthly budget the agent is paused and the CEO is told.
/// Switch <c>api.agents</c> off → 423 for these endpoints.
/// </summary>
public interface IAgentSelfService
{
    Task<AgentMeDto> MeAsync(CancellationToken ct);

    Task<IReadOnlyList<AgentTaskDto>> TasksAsync(CancellationToken ct);

    Task<AgentRunDto> StartRunAsync(StartRunRequest request, CancellationToken ct);

    Task<AgentRunDto> FinishRunAsync(long runId, FinishRunRequest request, CancellationToken ct);
}

public sealed class AgentSelfService(
    IAgentRepository agents,
    ICurrentAgent currentAgent,
    IFeatureFlags flags,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IClock clock) : IAgentSelfService
{
    public const string ApiGroup = "api.agents";

    private AgentCaller Caller => currentAgent.Caller ?? throw new ForbiddenException("Only an agent API key can call this.", "agent_only");

    public async Task<AgentMeDto> MeAsync(CancellationToken ct)
    {
        var card = await CardAsync(ct);
        return new AgentMeDto(card.Id, card.Name, card.TitleEn, card.Status, card.Model, card.Permissions, card.MoneyLimit, card.MonthlyBudget,
            card.CostThisMonthUsd, Caller.KeyPrefix, Caller.RateLimitPerMinute);
    }

    public async Task<IReadOnlyList<AgentTaskDto>> TasksAsync(CancellationToken ct)
    {
        await flags.EnsureOnAsync(ApiGroup, ct);
        return await agents.TasksAsync(Caller.AgentId, ct);
    }

    public async Task<AgentRunDto> StartRunAsync(StartRunRequest request, CancellationToken ct)
    {
        await flags.EnsureOnAsync(ApiGroup, ct);
        if (request.TaskId is { } taskId && !await agents.TaskBelongsAsync(Caller.AgentId, taskId, ct))
        {
            throw new NotFoundException("This task is not one of yours.");
        }
        var run = new AgentRun { AgentId = Caller.AgentId, TaskId = request.TaskId, StartedAt = clock.UtcNow, Result = AgentRunResult.Running, Summary = request.Summary?.Trim() };
        agents.Add(run);
        await unitOfWork.SaveChangesAsync(ct);
        return ToDto(run);
    }

    public async Task<AgentRunDto> FinishRunAsync(long runId, FinishRunRequest request, CancellationToken ct)
    {
        await flags.EnsureOnAsync(ApiGroup, ct);
        var run = await agents.RunAsync(Caller.AgentId, runId, ct) ?? throw new NotFoundException("Run not found.");
        if (run.FinishedAt is not null)
        {
            throw new ConflictException("This run is already finished.", "run_finished");
        }
        run.Result = Enum.Parse<AgentRunResult>(request.Result, true);
        run.Summary = request.Summary?.Trim() ?? run.Summary;
        (run.InputTokens, run.OutputTokens, run.CostUsd) = (request.InputTokens, request.OutputTokens, request.CostUsd);
        run.FinishedAt = run.Result == AgentRunResult.Running ? null : clock.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        await CheckBudgetAsync(ct);
        return ToDto(run);
    }

    /// <summary>Over the monthly budget → paused, the CEO told (the run itself is kept).</summary>
    private async Task CheckBudgetAsync(CancellationToken ct)
    {
        var agent = (await agents.GetAsync(Caller.AgentId, ct))!;
        var cost = await agents.CostThisMonthAsync(agent.Id, clock.UtcNow, ct);
        if (agent.MonthlyBudget <= 0 || cost <= agent.MonthlyBudget || agent.Status != AgentStatus.Active)
        {
            return;
        }
        (agent.Status, agent.PausedReason) = (AgentStatus.Paused, $"Monthly budget reached ({cost:0.##} of {agent.MonthlyBudget:0.##} USD).");
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.ApiOrAgent, "agent.budget_paused", $"{agent.Name} paused: {agent.PausedReason}", "Agent", agent.Id.ToString(), ct: ct);
        await notifier.NotifyStaffAsync("audit.read", new NotificationMessage(NotificationCategory.Account, $"Agent {agent.Name} paused",
            agent.PausedReason, "#/ceo/control", NotificationTone.Warning), ct);
    }

    private async Task<AgentDto> CardAsync(CancellationToken ct) =>
        await agents.CardAsync(Caller.AgentId, clock.UtcNow, ct) ?? throw new NotFoundException("This agent does not exist.");

    private static AgentRunDto ToDto(AgentRun r) =>
        new(r.Id, r.TaskId, r.StartedAt, r.FinishedAt, r.Result.ToString(), r.Summary, r.InputTokens, r.OutputTokens, r.CostUsd);
}
