using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Application.Features.CeoSystem;

/// <summary>Reads and flips the switches in ops.FeatureFlags.</summary>
public interface ISwitchRepository
{
    Task<IReadOnlyList<SwitchDto>> ListAsync(Domain.Enums.FeatureFlagKind? kind, CancellationToken ct);

    Task<SwitchDto?> GetAsync(string key, CancellationToken ct);

    Task<bool> IsOnAsync(string key, CancellationToken ct);

    /// <summary>Sets the switch and saves at once. False when the key does not exist.</summary>
    Task<bool> SetAsync(string key, bool enabled, string? reason, Guid? byUserId, CancellationToken ct);
}

/// <summary>Departments and positions of the org chart.</summary>
public interface IOrgRepository
{
    Task<OrgChartDto> ChartAsync(CancellationToken ct);

    Task<OrgPosition?> PositionAsync(long id, CancellationToken ct);

    Task<bool> DepartmentExistsAsync(int id, CancellationToken ct);

    Task<bool> PersonExistsAsync(Guid userId, CancellationToken ct);

    Task<bool> HasReportsAsync(long id, CancellationToken ct);

    /// <summary>True when <paramref name="newParentId"/> is the position itself or one of its reports (a loop).</summary>
    Task<bool> WouldLoopAsync(long id, long newParentId, CancellationToken ct);

    /// <summary>Agents sitting in the position lose it (they stay on the AI panel).</summary>
    Task DetachAgentsAsync(long positionId, CancellationToken ct);

    void Add(OrgPosition position);

    void Remove(OrgPosition position);
}

/// <summary>The integrations registry and its usage.</summary>
public interface IIntegrationRepository
{
    Task<IReadOnlyList<IntegrationDto>> ListAsync(DateOnly today, CancellationToken ct);

    Task<IntegrationDto?> DetailAsync(string key, DateOnly today, CancellationToken ct);

    Task<Integration?> GetAsync(string key, CancellationToken ct);

    /// <summary>Only one primary per category: the others lose the flag.</summary>
    Task ClearPrimaryAsync(Domain.Enums.IntegrationCategory category, int exceptId, CancellationToken ct);
}

/// <summary>Whether a secret is present in the server's environment (secrets are never stored in the database).</summary>
public interface ISecretPresence
{
    bool IsSet(string? environmentVariable);
}

/// <summary>Numbers for the AI panel and the API catalogue.</summary>
public interface IAiPanelReader
{
    Task<AiPanelDto> PanelAsync(bool agentsSwitchOn, DateTime now, CancellationToken ct);

    /// <summary>Agent calls per API group in the last 24 hours (from the audit log).</summary>
    Task<IReadOnlyDictionary<string, int>> AgentCallsByGroupAsync(DateTime since, CancellationToken ct);
}
