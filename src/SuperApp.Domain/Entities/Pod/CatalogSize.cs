using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A size of a printable product with its size chart (cm). Table <c>pod.CatalogSizes</c>.</summary>
public class CatalogSize : BaseEntity
{
    public int CatalogProductId { get; set; }
    public string Size { get; set; } = string.Empty;
    public bool IsBigSize { get; set; }
    public decimal ChestCm { get; set; }
    public decimal LengthCm { get; set; }
    public decimal SleeveCm { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public CatalogProduct CatalogProduct { get; set; } = null!;
}
