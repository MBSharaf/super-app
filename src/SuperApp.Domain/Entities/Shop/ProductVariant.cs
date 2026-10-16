using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A size/colour variant with its stock. Table <c>shop.ProductVariants</c>.</summary>
public class ProductVariant : BaseEntity, IHasRowVersion
{
    public long ProductId { get; set; }
    public string Size { get; set; } = "One size";
    public string ColorName { get; set; } = "Standard";
    public string? ColorHex { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int Stock { get; set; }
    public int Reserved { get; set; }
    public decimal? PriceOverride { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Product Product { get; set; } = null!;
}
