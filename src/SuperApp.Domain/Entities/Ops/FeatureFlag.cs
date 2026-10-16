using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>A CEO kill switch or an API group switch. Table <c>ops.FeatureFlags</c>.</summary>
public class FeatureFlag : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public FeatureFlagKind Kind { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? Reason { get; set; }
    public Guid? ChangedById { get; set; }
}
