namespace SuperApp.Application.Features.Agents;

/// <summary>An agent in the CEO's AI panel: who it is, what it may do, and how it is doing (runs, cost, errors).</summary>
public sealed record AgentDto(long Id, string Name, string TitleEn, string TitleAr, string Description, string Model, string Platform,
    string Status, string? PausedReason, decimal MoneyLimit, decimal MonthlyBudget, IReadOnlyList<string> Permissions,
    long? PositionId, Guid ServiceUserId, int ActiveKeys, int Runs30d, int Errors30d, decimal CostThisMonthUsd, DateTime? LastRunAt);

/// <summary>An API key as listed (the key itself is never shown again after it was made).</summary>
public sealed record ApiKeyDto(long Id, string Name, string Prefix, int RateLimitPerMinute, DateTime CreatedAt, DateTime? ExpiresAt,
    DateTime? RevokedAt, DateTime? LastUsedAt);

public sealed record AgentTaskDto(long Id, string Title, string Description, string Trigger, string? EventType, string? Cron,
    string? ExpectedOutput, bool IsEnabled, DateTime? LastRunAt);

public sealed record AgentRunDto(long Id, long? TaskId, DateTime StartedAt, DateTime? FinishedAt, string Result, string? Summary,
    int InputTokens, int OutputTokens, decimal CostUsd);

/// <summary>The agent's page: the card plus keys, tasks and the latest 20 runs.</summary>
public sealed record AgentDetailDto(AgentDto Agent, IReadOnlyList<ApiKeyDto> Keys, IReadOnlyList<AgentTaskDto> Tasks, IReadOnlyList<AgentRunDto> Runs);

/// <summary>POST /ceo/agents — a service account is made for it; it starts Paused with no key.</summary>
public sealed record CreateAgentRequest(string Name, string TitleEn, string TitleAr, string Description, string Model, string Platform,
    decimal MoneyLimit, decimal MonthlyBudget, IReadOnlyList<string> Permissions, long? PositionId);

public sealed record UpdateAgentRequest(string Name, string TitleEn, string TitleAr, string Description, string Model, string Platform,
    decimal MoneyLimit, decimal MonthlyBudget, long? PositionId);

public sealed record AgentPermissionsRequest(IReadOnlyList<string> Permissions);

public sealed record AgentTaskInput(string Title, string Description, string Trigger, string? EventType, string? Cron, string? ExpectedOutput, bool IsEnabled = true);

/// <summary>PUT /ceo/agents/{id}/tasks — the full list replaces the old one.</summary>
public sealed record AgentTasksRequest(IReadOnlyList<AgentTaskInput> Tasks);

public sealed record ReasonRequest(string Reason);

/// <summary>POST /ceo/agents/{id}/keys — default 600 calls a minute, no expiry.</summary>
public sealed record CreateApiKeyRequest(string Name, int? RateLimitPerMinute, DateTime? ExpiresAt);

/// <summary>The new key, shown once: copy it into the n8n credential (header <c>X-Api-Key</c>).</summary>
public sealed record NewApiKeyDto(long Id, string Name, string Prefix, string Key, int RateLimitPerMinute, DateTime? ExpiresAt, string Header);

public sealed record EmergencyResultDto(bool Stopped, int AgentsAffected);

/// <summary>GET /agent/me — what the calling key's agent is and may do.</summary>
public sealed record AgentMeDto(long Id, string Name, string TitleEn, string Status, string Model, IReadOnlyList<string> Permissions,
    decimal MoneyLimit, decimal MonthlyBudget, decimal CostThisMonthUsd, string KeyPrefix, int RateLimitPerMinute);

/// <summary>POST /agent/runs — a run starts (Running).</summary>
public sealed record StartRunRequest(long? TaskId, string? Summary);

/// <summary>PATCH /agent/runs/{id} — the result and what it cost.</summary>
public sealed record FinishRunRequest(string Result, string? Summary, int InputTokens, int OutputTokens, decimal CostUsd);

/// <summary>The caller when it is an agent key (filled by the API-key sign-in).</summary>
public sealed record AgentCaller(long AgentId, long KeyId, string KeyPrefix, int RateLimitPerMinute);

/// <summary>What the API-key sign-in needs to know about a key.</summary>
public sealed record AgentKeyIdentity(long KeyId, long AgentId, Guid ServiceUserId, string AgentName, string Prefix, byte[] Hash,
    string AgentStatus, DateTime? ExpiresAt, DateTime? RevokedAt, DateTime? LastUsedAt, int RateLimitPerMinute, IReadOnlyList<string> Permissions);
