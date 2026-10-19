using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.CeoSystem;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Agents;

/// <summary>
/// The CEO's AI panel (API §8 "AI agents"): create and edit agents, their permissions and tasks, pause / resume,
/// issue and revoke API keys, and the emergency stop for every agent (switch <c>kill.agents</c>). Every change is audited.
/// </summary>
public interface IAgentsService
{
    Task<IReadOnlyList<AgentDto>> ListAsync(CancellationToken ct);

    Task<AgentDetailDto> GetAsync(long id, CancellationToken ct);

    Task<AgentDetailDto> CreateAsync(CreateAgentRequest request, CancellationToken ct);

    Task<AgentDetailDto> UpdateAsync(long id, UpdateAgentRequest request, CancellationToken ct);

    Task<AgentDetailDto> SetPermissionsAsync(long id, AgentPermissionsRequest request, CancellationToken ct);

    Task<AgentDetailDto> SetTasksAsync(long id, AgentTasksRequest request, CancellationToken ct);

    Task<AgentDetailDto> PauseAsync(long id, ReasonRequest request, CancellationToken ct);

    Task<AgentDetailDto> ResumeAsync(long id, CancellationToken ct);

    Task<NewApiKeyDto> CreateKeyAsync(long id, CreateApiKeyRequest request, CancellationToken ct);

    Task<AgentDetailDto> RevokeKeyAsync(long id, long keyId, CancellationToken ct);

    Task<EmergencyResultDto> EmergencyStopAsync(ReasonRequest request, CancellationToken ct);

    Task<EmergencyResultDto> EmergencyReleaseAsync(CancellationToken ct);

    Task DeleteAsync(long id, CancellationToken ct);
}

public sealed partial class AgentsService(
    IAgentRepository agents,
    ISwitchRepository switches,
    IUnitOfWork unitOfWork,
    IAuditLogger audit,
    INotifier notifier,
    ICurrentUser currentUser,
    IClock clock) : IAgentsService
{
    public Task<IReadOnlyList<AgentDto>> ListAsync(CancellationToken ct) => agents.ListAsync(clock.UtcNow, ct);

    public async Task<AgentDetailDto> GetAsync(long id, CancellationToken ct) =>
        await agents.DetailAsync(id, clock.UtcNow, ct) ?? throw new NotFoundException("This agent does not exist.");

    public async Task<AgentDetailDto> CreateAsync(CreateAgentRequest request, CancellationToken ct)
    {
        await EnsureNameFreeAsync(request.Name, null, ct);
        await EnsurePositionAsync(request.PositionId, null, ct);
        var agent = new Agent
        {
            ServiceUserId = await agents.CreateServiceUserAsync(request.Name.Trim(), ct),
            Status = AgentStatus.Paused,
            PausedReason = "New agent — give it an API key, then resume it.",
        };
        Apply(agent, new UpdateAgentRequest(request.Name, request.TitleEn, request.TitleAr, request.Description, request.Model,
            request.Platform, request.MoneyLimit, request.MonthlyBudget, request.PositionId));
        agents.Add(agent);
        await unitOfWork.SaveChangesAsync(ct);
        await agents.SetPermissionsAsync(agent, request.Permissions.Distinct().ToList(), ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.created", $"Agent {agent.Name} created ({string.Join(", ", request.Permissions)})", "Agent", agent.Id.ToString(), request, ct);
        return await GetAsync(agent.Id, ct);
    }

    public async Task<AgentDetailDto> UpdateAsync(long id, UpdateAgentRequest request, CancellationToken ct)
    {
        var agent = await AgentAsync(id, ct);
        await EnsureNameFreeAsync(request.Name, id, ct);
        await EnsurePositionAsync(request.PositionId, id, ct);
        Apply(agent, request);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.updated", $"Agent {agent.Name} edited", "Agent", id.ToString(), request, ct);
        return await GetAsync(id, ct);
    }

    public async Task<AgentDetailDto> SetPermissionsAsync(long id, AgentPermissionsRequest request, CancellationToken ct)
    {
        var agent = await AgentAsync(id, ct);
        await agents.SetPermissionsAsync(agent, request.Permissions.Distinct().ToList(), ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.permissions", $"{agent.Name}: {string.Join(", ", request.Permissions)}", "Agent", id.ToString(), request, ct);
        return await GetAsync(id, ct);
    }

    public async Task<AgentDetailDto> SetTasksAsync(long id, AgentTasksRequest request, CancellationToken ct)
    {
        var agent = await AgentAsync(id, ct);
        var tasks = request.Tasks.Select(t => new AgentTask
        {
            AgentId = id, Title = t.Title.Trim(), Description = t.Description.Trim(), Trigger = Enum.Parse<AgentTrigger>(t.Trigger, true),
            EventType = t.EventType?.Trim(), Cron = t.Cron?.Trim(), ExpectedOutput = t.ExpectedOutput?.Trim(), IsEnabled = t.IsEnabled,
        }).ToList();
        await agents.ReplaceTasksAsync(id, tasks, ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.tasks", $"{agent.Name}: {tasks.Count} tasks", "Agent", id.ToString(), request, ct);
        return await GetAsync(id, ct);
    }

    private static void Apply(Agent agent, UpdateAgentRequest request)
    {
        agent.Name = request.Name.Trim();
        agent.TitleEn = request.TitleEn.Trim();
        agent.TitleAr = request.TitleAr.Trim();
        agent.Description = request.Description.Trim();
        agent.Model = request.Model.Trim();
        agent.Platform = Enum.Parse<AgentPlatform>(request.Platform, true);
        agent.MoneyLimit = request.MoneyLimit;
        agent.MonthlyBudget = request.MonthlyBudget;
        agent.PositionId = request.PositionId;
    }

    private async Task EnsureNameFreeAsync(string name, long? exceptId, CancellationToken ct)
    {
        if (await agents.NameTakenAsync(name.Trim(), exceptId, ct))
        {
            throw new ConflictException("Another agent already has this name.", "name_taken");
        }
    }

    private async Task EnsurePositionAsync(long? positionId, long? agentId, CancellationToken ct)
    {
        if (positionId is not { } p)
        {
            return;
        }
        if (!await agents.PositionExistsAsync(p, ct))
        {
            throw new BadRequestException("This position is not on the org chart.", "positionId");
        }
        if (await agents.PositionTakenAsync(p, agentId, ct))
        {
            throw new ConflictException("Another agent already holds this position.", "position_taken");
        }
    }

    private async Task<Agent> AgentAsync(long id, CancellationToken ct) =>
        await agents.GetAsync(id, ct) ?? throw new NotFoundException("This agent does not exist.");
}
