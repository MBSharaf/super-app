using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A product photo. Table <c>shop.ProductImages</c>.</summary>
public class ProductImage : BaseEntity
{
    public long ProductId { get; set; }
    public long FileId { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;
    public StoredFile File { get; set; } = null!;
}
