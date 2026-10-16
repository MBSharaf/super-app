using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>Squads in a fantasy league. Table <c>fantasy.GroupMembers</c>.</summary>
public class GroupMember
{
    public long GroupId { get; set; }
    public long TeamId { get; set; }
    public int? Rank { get; set; }
    public DateTime? PaidUntil { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public FantasyGroup Group { get; set; } = null!;
    public FantasyTeam Team { get; set; } = null!;
}
