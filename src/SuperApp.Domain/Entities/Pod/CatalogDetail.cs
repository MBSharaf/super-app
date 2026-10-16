using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A product detail line (fabric, fit, print, care…). Table <c>pod.CatalogDetails</c>.</summary>
public class CatalogDetail : BaseEntity
{
    public int CatalogProductId { get; set; }
    public string LabelEn { get; set; } = string.Empty;
    public string LabelAr { get; set; } = string.Empty;
    public string ValueEn { get; set; } = string.Empty;
    public string ValueAr { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    // Navigation
    public CatalogProduct CatalogProduct { get; set; } = null!;
}
