using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ai;

/// <summary>A task an agent runs (on an event, a schedule or by hand). Table <c>ai.AgentTasks</c>.</summary>
public class AgentTask : BaseEntity
{
    public long AgentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AgentTrigger Trigger { get; set; }
    public string? EventType { get; set; }
    public string? Cron { get; set; }
    public string? ExpectedOutput { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime? LastRunAt { get; set; }

    // Navigation
    public Agent Agent { get; set; } = null!;
}
