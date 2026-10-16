using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>Customers following a store. Table <c>pod.SellerFollowers</c>.</summary>
public class SellerFollower
{
    public long SellerId { get; set; }
    public Guid UserId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Seller Seller { get; set; } = null!;
    public User User { get; set; } = null!;
}
