using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A staff account of a pitch owner. Table <c>pitch.PartnerStaff</c>.</summary>
public class PartnerStaffMember : BaseEntity
{
    public long PartnerId { get; set; }
    public Guid UserId { get; set; }
    public PartnerStaffRole Role { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Partner Partner { get; set; } = null!;
    public User User { get; set; } = null!;
}
