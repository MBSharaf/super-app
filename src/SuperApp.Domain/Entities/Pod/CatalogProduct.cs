using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A printable product made by the factory (tee, oversized tee, hoodie). Table <c>pod.CatalogProducts</c>.</summary>
public class CatalogProduct : IntBaseEntity, ISoftDelete
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public GarmentKind Kind { get; set; }
    public string DescriptionEn { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public decimal BasePrice { get; set; }
    public decimal FactoryCost { get; set; }
    public decimal BackPrintExtra { get; set; }
    public decimal BigSizeExtra { get; set; }
    public decimal MaxPrice { get; set; }
    public bool HasBackPrint { get; set; } = true;
    public string? Model3dUrl { get; set; }
    public CatalogProductStatus Status { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public ICollection<CatalogColor> Colors { get; set; } = [];
    public ICollection<CatalogSize> Sizes { get; set; } = [];
    public ICollection<CatalogDetail> Details { get; set; } = [];
    public ICollection<CatalogPrintArea> PrintAreas { get; set; } = [];
}
