using System.Text.Json;

namespace SuperApp.Application.Features.CeoSystem;

/// <summary>A kill switch or an API group switch (ops.FeatureFlags).</summary>
public sealed record SwitchDto(string Key, string Kind, bool IsEnabled, string? Reason, string? ChangedBy, DateTime? ChangedAt);

/// <summary>PUT /ceo/switches/{key} — switching off needs a reason (it is shown to the people who hit the switch).</summary>
public sealed record SetSwitchRequest(bool Enabled, string? Reason);

public sealed record DepartmentDto(int Id, string Key, string NameEn, string NameAr, string Color, int SortOrder);

/// <summary>Who holds a box: a person, an AI agent, or nobody (vacant).</summary>
public sealed record HolderDto(string Kind, Guid? UserId, string? Name, long? AgentId, string? AgentStatus);

public sealed record PositionDto(long Id, int? DepartmentId, long? ReportsToId, string TitleEn, string TitleAr, HolderDto Holder,
    IReadOnlyList<string> Duties, int SortOrder);

/// <summary>GET /ceo/org — the pyramid.</summary>
public sealed record OrgChartDto(IReadOnlyList<DepartmentDto> Departments, IReadOnlyList<PositionDto> Positions);

/// <summary>POST / PUT /ceo/org/positions — the holder is a person's account (an agent sits in its position from the AI panel).</summary>
public sealed record PositionRequest(int? DepartmentId, long? ReportsToId, string TitleEn, string TitleAr, Guid? HolderUserId,
    IReadOnlyList<string>? Duties, int SortOrder);

public sealed record UsageDayDto(DateOnly Day, int Calls, int Errors, int? QuotaUsed, int? QuotaLimit);

/// <summary>One integration in the registry. Secrets live only in the server's environment; <c>SecretSet</c> says whether it is there.</summary>
public sealed record IntegrationDto(string Key, string Category, string Name, string Mode, bool IsEnabled, bool IsPrimary, string Status,
    string? SecretName, bool SecretSet, DateTime? LastCallAt, DateTime? LastErrorAt, string? LastError, int? DailyQuota,
    UsageDayDto? Today, JsonElement? Config, IReadOnlyList<UsageDayDto>? Usage);

/// <summary>PUT /ceo/integrations/{key} — mode Simulated / Test / Live; primary = the one used for its category.</summary>
public sealed record UpdateIntegrationRequest(string Mode, bool IsEnabled, bool IsPrimary, JsonElement? Config);

/// <summary>One endpoint group of the API catalogue with its 24-hour agent traffic.</summary>
public sealed record ApiGroupDto(string Group, string Name, string Purpose, string Permission, string Callers, string Path,
    string? SwitchKey, bool? Enabled, int AgentCalls24h);

public sealed record SetApiGroupRequest(bool Enabled, string? Reason);

public sealed record AgentCostDto(long AgentId, string Name, string Status, decimal CostThisMonthUsd, decimal MonthlyBudget, int Runs30d, int Errors30d);

public sealed record FailedRunDto(long RunId, long AgentId, string Agent, DateTime StartedAt, string? Summary);

/// <summary>GET /ceo/ai — the AI panel summary: agents by status, runs, cost, errors.</summary>
public sealed record AiPanelDto(bool AgentsSwitchOn, int Agents, int Active, int Paused, int EmergencyStopped, int ActiveKeys,
    int RunsToday, int Runs30d, int Errors30d, decimal CostThisMonthUsd, decimal MonthlyBudgetUsd,
    IReadOnlyList<AgentCostDto> ByAgent, IReadOnlyList<FailedRunDto> RecentErrors);

/// <summary>An Excel file to send back.</summary>
public sealed record ExcelFile(string FileName, byte[] Content)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}

public sealed record ReportQuery(DateOnly? From, DateOnly? To);
