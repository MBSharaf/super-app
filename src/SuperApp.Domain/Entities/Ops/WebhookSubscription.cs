using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>An outgoing webhook (e.g. to n8n). Table <c>ops.WebhookSubscriptions</c>.</summary>
public class WebhookSubscription : IntBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Events { get; set; } = string.Empty;
    public byte[] SecretHash { get; set; } = [];
    public bool IsEnabled { get; set; } = true;
    public int FailureCount { get; set; }

    // Navigation
    public ICollection<WebhookDelivery> Deliveries { get; set; } = [];
}
