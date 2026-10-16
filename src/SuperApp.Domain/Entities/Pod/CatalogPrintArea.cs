using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>The print area of one side of a product. Table <c>pod.CatalogPrintAreas</c>.</summary>
public class CatalogPrintArea : BaseEntity
{
    public int CatalogProductId { get; set; }
    public PrintSide Side { get; set; }
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public decimal PrintWidthCm { get; set; } = 30m;

    // Navigation
    public CatalogProduct CatalogProduct { get; set; } = null!;
}
