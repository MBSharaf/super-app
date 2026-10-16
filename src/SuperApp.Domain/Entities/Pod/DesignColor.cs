using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>Colours a customer can choose for a design. Table <c>pod.DesignColors</c>.</summary>
public class DesignColor
{
    public long DesignId { get; set; }
    public long CatalogColorId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Design Design { get; set; } = null!;
    public CatalogColor CatalogColor { get; set; } = null!;
}
