using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>A measurable result of a goal. Table <c>ops.KeyResults</c>.</summary>
public class KeyResult : BaseEntity
{
    public long GoalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Metric { get; set; }
    public decimal Target { get; set; }
    public decimal Current { get; set; }

    // Navigation
    public Goal Goal { get; set; } = null!;
}
