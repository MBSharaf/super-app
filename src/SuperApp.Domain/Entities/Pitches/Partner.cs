using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A pitch owner's business profile. Table <c>pitch.Partners</c>.</summary>
public class Partner : BaseEntity, ISoftDelete
{
    public Guid UserId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public decimal? BookingCommission { get; set; }
    public decimal? StoreCommission { get; set; }
    public PayoutMethod PayoutMethod { get; set; }
    /// <summary>Encrypted payout account.</summary>
    public string PayoutDetail { get; set; } = string.Empty;
    public PartnerStatus Status { get; set; }
    public string? SuspendedReason { get; set; }
    public bool SlotPostingBlocked { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<PartnerStaffMember> Staff { get; set; } = [];
}
