using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A seller (print on demand and/or own products). Table <c>pod.Sellers</c>.</summary>
public class Seller : BaseEntity, ISoftDelete, IHasRowVersion
{
    public Guid UserId { get; set; }
    public long StoreId { get; set; }
    public int LevelId { get; set; }
    public SellerMode Mode { get; set; }
    public SellerStatus Status { get; set; }
    public DateTime? FrozenUntil { get; set; }
    public DateOnly? GraceUntil { get; set; }
    public int LifetimeSales { get; set; }
    public decimal Rating { get; set; }
    public long? FeaturedDesignId { get; set; }
    public bool FeaturedByAdmin { get; set; }
    public string? Tags { get; set; }
    public PayoutMethod PayoutMethod { get; set; }
    /// <summary>Encrypted payout account.</summary>
    public string PayoutDetail { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public User User { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public SellerLevel Level { get; set; } = null!;
    public Design? FeaturedDesign { get; set; }
    public ICollection<LevelReview> LevelReviews { get; set; } = [];
    public ICollection<SellerFollower> Followers { get; set; } = [];
}
