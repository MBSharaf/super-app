using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A package size (1–12 matches) a pitch offers, with its discount. Table <c>pitch.PackageOptions</c>.</summary>
public class PackageOption : BaseEntity
{
    public long PitchId { get; set; }
    public byte MatchCount { get; set; }
    public decimal DiscountPercent { get; set; }
    public PackageOptionStatus Status { get; set; }
    public decimal? ApprovedDiscount { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
