using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>The monthly level review of a seller. Table <c>pod.LevelReviews</c>.</summary>
public class LevelReview : BaseEntity
{
    public long SellerId { get; set; }
    public DateOnly Month { get; set; }
    public int FromLevelId { get; set; }
    public int ToLevelId { get; set; }
    public LevelReviewAction Action { get; set; }
    public string? Note { get; set; }

    // Navigation
    public Seller Seller { get; set; } = null!;
    public SellerLevel FromLevel { get; set; } = null!;
    public SellerLevel ToLevel { get; set; } = null!;
}
