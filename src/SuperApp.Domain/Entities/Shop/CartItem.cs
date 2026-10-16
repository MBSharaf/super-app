using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A line in a customer's cart. Table <c>shop.CartItems</c>.</summary>
public class CartItem : BaseEntity
{
    public Guid UserId { get; set; }
    public long? ProductVariantId { get; set; }
    public long? DesignId { get; set; }
    public string? Size { get; set; }
    public string? Color { get; set; }
    public int Qty { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ProductVariant? ProductVariant { get; set; }
    public Design? Design { get; set; }
}
