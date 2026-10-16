using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A specification line of a product. Table <c>shop.ProductSpecs</c>.</summary>
public class ProductSpec : Entity<long>
{
    public long ProductId { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
}
