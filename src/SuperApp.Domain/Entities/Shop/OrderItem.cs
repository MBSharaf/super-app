using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A line of an order: a store product or a printed design. Table <c>shop.OrderItems</c>.</summary>
public class OrderItem : BaseEntity
{
    public long OrderId { get; set; }
    public OrderItemKind Kind { get; set; }
    public long StoreId { get; set; }
    public long? ProductVariantId { get; set; }
    public long? DesignId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public int Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? BaseAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal SellerEarning { get; set; }
    public OrderItemStatus Status { get; set; }
    public DateTime? DeliveredAt { get; set; }

    // Navigation
    public Order Order { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
    public Design? Design { get; set; }
}
