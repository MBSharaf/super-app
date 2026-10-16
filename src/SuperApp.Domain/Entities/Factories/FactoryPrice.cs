using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Domain.Entities.Factories;

/// <summary>What the factory charges per piece for a product. Table <c>factory.FactoryPrices</c>.</summary>
public class FactoryPrice : BaseEntity
{
    public long FactoryId { get; set; }
    public int CatalogProductId { get; set; }
    public decimal PricePerPiece { get; set; }
    public decimal BackPrintPrice { get; set; }

    // Navigation
    public Factory Factory { get; set; } = null!;
    public CatalogProduct CatalogProduct { get; set; } = null!;
}
