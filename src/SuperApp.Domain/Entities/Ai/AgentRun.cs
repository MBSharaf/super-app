using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ai;

/// <summary>One run of an agent with its result and cost. Table <c>ai.AgentRuns</c>.</summary>
public class AgentRun : Entity<long>
{
    public long AgentId { get; set; }
    public long? TaskId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public AgentRunResult Result { get; set; }
    public string? Summary { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal CostUsd { get; set; }

    // Navigation
    public Agent Agent { get; set; } = null!;
    public AgentTask? Task { get; set; }
}
