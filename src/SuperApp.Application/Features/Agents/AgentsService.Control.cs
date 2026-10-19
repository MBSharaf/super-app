using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Agents;

/// <summary>Pause / resume, keys, emergency stop and delete.</summary>
public sealed partial class AgentsService
{
    public async Task<AgentDetailDto> PauseAsync(long id, ReasonRequest request, CancellationToken ct)
    {
        var agent = await AgentAsync(id, ct);
        (agent.Status, agent.PausedReason) = (AgentStatus.Paused, request.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.paused", $"{agent.Name} paused: {request.Reason}", "Agent", id.ToString(), ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<AgentDetailDto> ResumeAsync(long id, CancellationToken ct)
    {
        var agent = await AgentAsync(id, ct);
        if (!await switches.IsOnAsync(Abstractions.Services.KillSwitches.Agents, ct))
        {
            throw new ConflictException("Every agent is stopped (emergency stop). Release them first.", "agents_stopped");
        }
        (agent.Status, agent.PausedReason) = (AgentStatus.Active, null);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.resumed", $"{agent.Name} resumed", "Agent", id.ToString(), ct: ct);
        return await GetAsync(id, ct);
    }

    public async Task<NewApiKeyDto> CreateKeyAsync(long id, CreateApiKeyRequest request, CancellationToken ct)
    {
        var agent = await AgentAsync(id, ct);
        var secret = ApiKeys.NewKey();
        while (await agents.PrefixTakenAsync(ApiKeys.PrefixOf(secret), ct))
        {
            secret = ApiKeys.NewKey();
        }
        var rate = request.RateLimitPerMinute ?? ApiKeys.DefaultRatePerMinute;
        var key = new ApiKey
        {
            AgentId = id, Name = request.Name.Trim(), Prefix = ApiKeys.PrefixOf(secret), KeyHash = ApiKeys.Hash(secret),
            Scopes = ApiKeys.ScopesFor(rate), ExpiresAt = request.ExpiresAt,
        };
        agents.Add(key);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.key_created", $"{agent.Name}: key {key.Prefix}… ({key.Name}, {rate}/min)", "ApiKey", key.Id.ToString(), ct: ct);
        return new NewApiKeyDto(key.Id, key.Name, key.Prefix, secret, rate, key.ExpiresAt, ApiKeys.Header);
    }

    public async Task<AgentDetailDto> RevokeKeyAsync(long id, long keyId, CancellationToken ct)
    {
        var agent = await AgentAsync(id, ct);
        var key = await agents.KeyAsync(id, keyId, ct) ?? throw new NotFoundException("This key does not exist.");
        if (key.RevokedAt is not null)
        {
            throw new ConflictException("This key is already revoked.", "already_revoked");
        }
        key.RevokedAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.key_revoked", $"{agent.Name}: key {key.Prefix}… revoked", "ApiKey", keyId.ToString(), ct: ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Switch <c>kill.agents</c> off: every key answers 423 at once, and running agents are marked EmergencyStopped.</summary>
    public async Task<EmergencyResultDto> EmergencyStopAsync(ReasonRequest request, CancellationToken ct)
    {
        await switches.SetAsync(Abstractions.Services.KillSwitches.Agents, false, request.Reason.Trim(), currentUser.UserId, ct);
        var count = await agents.SetAllStatusAsync(AgentStatus.Active, AgentStatus.EmergencyStopped, "Emergency stop: " + request.Reason.Trim(), ct);
        await audit.LogAsync(AuditSystem.Ceo, "agents.emergency_stop", $"Every agent stopped ({count} were running): {request.Reason}", "FeatureFlag", "kill.agents", ct: ct);
        await notifier.NotifyStaffAsync("audit.read", new NotificationMessage(NotificationCategory.Account, "Every AI agent was stopped",
            request.Reason.Trim(), "#/ceo/control", NotificationTone.Warning), ct);
        return new EmergencyResultDto(true, count);
    }

    /// <summary>Switch <c>kill.agents</c> on again; agents stopped by the emergency go back to Active, paused ones stay paused.</summary>
    public async Task<EmergencyResultDto> EmergencyReleaseAsync(CancellationToken ct)
    {
        await switches.SetAsync(Abstractions.Services.KillSwitches.Agents, true, null, currentUser.UserId, ct);
        var count = await agents.SetAllStatusAsync(AgentStatus.EmergencyStopped, AgentStatus.Active, null, ct);
        await audit.LogAsync(AuditSystem.Ceo, "agents.emergency_release", $"Agents released ({count} running again)", "FeatureFlag", "kill.agents", ct: ct);
        await notifier.NotifyStaffAsync("audit.read", new NotificationMessage(NotificationCategory.Account, "AI agents are running again",
            $"{count} agents resumed.", "#/ceo/control"), ct);
        return new EmergencyResultDto(false, count);
    }

    /// <summary>Soft delete: keys revoked, service account suspended, runs and audit history kept.</summary>
    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var agent = await AgentAsync(id, ct);
        var revoked = await agents.RevokeKeysAsync(id, clock.UtcNow, ct);
        (agent.IsDeleted, agent.DeletedAt, agent.Status, agent.PausedReason) = (true, clock.UtcNow, AgentStatus.Paused, "Deleted");
        await unitOfWork.SaveChangesAsync(ct);
        await agents.SetServiceUserActiveAsync(agent.ServiceUserId, false, ct);
        await audit.LogAsync(AuditSystem.Ceo, "agent.deleted", $"Agent {agent.Name} deleted ({revoked} keys revoked)", "Agent", id.ToString(), ct: ct);
    }
}
