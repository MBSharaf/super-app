using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A store product. Table <c>shop.Products</c>.</summary>
public class Product : BaseEntity, ISoftDelete, IHasAuthor
{
    public long StoreId { get; set; }
    public int CategoryId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public int LowStockAt { get; set; } = 5;
    public ProductStatus Status { get; set; }
    public bool HiddenByAdmin { get; set; }
    public string? HiddenReason { get; set; }
    public decimal Rating { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? CreatedById { get; set; }

    // Navigation
    public Store Store { get; set; } = null!;
    public ProductCategory Category { get; set; } = null!;
    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<ProductVariant> Variants { get; set; } = [];
    public ICollection<ProductSpec> Specs { get; set; } = [];
}
