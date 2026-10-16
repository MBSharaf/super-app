using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>An event saved with the change, delivered to webhooks by a background job. Table <c>ops.OutboxEvents</c>.</summary>
public class OutboxEvent : Entity<long>
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public OutboxStatus Status { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
}
