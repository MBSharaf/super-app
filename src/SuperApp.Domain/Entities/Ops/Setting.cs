using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>A platform setting stored as JSON (key/value). Table <c>ops.Settings</c>.</summary>
public class Setting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
