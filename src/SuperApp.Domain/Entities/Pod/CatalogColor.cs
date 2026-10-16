using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A colour of a printable product. Table <c>pod.CatalogColors</c>.</summary>
public class CatalogColor : BaseEntity
{
    public int CatalogProductId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    // Navigation
    public CatalogProduct CatalogProduct { get; set; } = null!;
}
