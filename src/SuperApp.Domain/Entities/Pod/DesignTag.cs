using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A search tag of a design. Table <c>pod.DesignTags</c>.</summary>
public class DesignTag
{
    public long DesignId { get; set; }
    public string Tag { get; set; } = string.Empty;

    // Navigation
    public Design Design { get; set; } = null!;
}
