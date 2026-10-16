using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>An external service in the integrations registry. Table <c>ops.Integrations</c>.</summary>
public class Integration : IntBaseEntity
{
    public string Key { get; set; } = string.Empty;
    public IntegrationCategory Category { get; set; }
    public string Name { get; set; } = string.Empty;
    public IntegrationMode Mode { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsPrimary { get; set; }
    public string? ConfigJson { get; set; }
    public string? SecretName { get; set; }
    public DateTime? SecretSetAt { get; set; }
    public IntegrationStatus Status { get; set; }
    public DateTime? LastCallAt { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public string? LastError { get; set; }
    public int? DailyQuota { get; set; }

    // Navigation
    public ICollection<IntegrationUsage> Usage { get; set; } = [];
}
