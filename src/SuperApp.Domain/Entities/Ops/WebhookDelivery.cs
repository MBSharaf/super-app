using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>One delivery attempt of an event to a webhook. Table <c>ops.WebhookDeliveries</c>.</summary>
public class WebhookDelivery : Entity<long>
{
    public int SubscriptionId { get; set; }
    public long OutboxEventId { get; set; }
    public byte Attempt { get; set; }
    public short? StatusCode { get; set; }
    public int? DurationMs { get; set; }
    public string? Error { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;

    // Navigation
    public WebhookSubscription Subscription { get; set; } = null!;
    public OutboxEvent OutboxEvent { get; set; } = null!;
}
