using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ai;

/// <summary>An AI agent: a service account with permissions, limits and a model. Table <c>ai.Agents</c>.</summary>
public class Agent : BaseEntity, ISoftDelete
{
    public Guid ServiceUserId { get; set; }
    public long? PositionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public AgentPlatform Platform { get; set; }
    public decimal MoneyLimit { get; set; }
    public decimal MonthlyBudget { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Paused;
    public string? PausedReason { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public User ServiceUser { get; set; } = null!;
    public OrgPosition? Position { get; set; }
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
    public ICollection<AgentTask> Tasks { get; set; } = [];
    public ICollection<AgentPermission> Permissions { get; set; } = [];
}
