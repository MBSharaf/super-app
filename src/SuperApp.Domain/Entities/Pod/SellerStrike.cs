using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A copyright strike on a seller. Table <c>pod.SellerStrikes</c>.</summary>
public class SellerStrike : BaseEntity, IHasAuthor
{
    public long SellerId { get; set; }
    public long? DesignId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RemovedAt { get; set; }
    public string? RemovedReason { get; set; }
    public Guid? CreatedById { get; set; }

    // Navigation
    public Seller Seller { get; set; } = null!;
    public Design? Design { get; set; }
}
