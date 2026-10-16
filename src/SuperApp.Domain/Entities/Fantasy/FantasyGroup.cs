using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A fantasy league: official, private or paid (paid ones hidden until payments are live). Table <c>fantasy.FantasyGroups</c>.</summary>
public class FantasyGroup : BaseEntity, ISoftDelete
{
    public long SeasonId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string InviteCode { get; set; } = string.Empty;
    public FantasyGroupType Type { get; set; }
    public bool IsHidden { get; set; }
    public decimal? MonthlyPrice { get; set; }
    public Guid? OwnerUserId { get; set; }
    public long? SponsorId { get; set; }
    public string? PrizeText { get; set; }
    public decimal PrizeValue { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public Season Season { get; set; } = null!;
    public User? OwnerUser { get; set; }
    public Sponsor? Sponsor { get; set; }
    public ICollection<GroupMember> Members { get; set; } = [];
}
