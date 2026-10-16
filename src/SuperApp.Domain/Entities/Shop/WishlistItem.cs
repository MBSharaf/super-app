using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A saved product or design. Table <c>shop.WishlistItems</c>.</summary>
public class WishlistItem : BaseEntity
{
    public Guid UserId { get; set; }
    public long? ProductId { get; set; }
    public long? DesignId { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Product? Product { get; set; }
    public Design? Design { get; set; }
}
