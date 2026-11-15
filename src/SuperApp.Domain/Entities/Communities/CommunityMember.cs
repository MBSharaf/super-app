using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>Members of a community. Table <c>community.CommunityMembers</c>.</summary>
public class CommunityMember
{
    public long CommunityId { get; set; }
    public Guid UserId { get; set; }
    public CommunityRole Role { get; set; }
    public DateTime? MutedUntil { get; set; }
    public string? MuteReason { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Community Community { get; set; } = null!;
    public User User { get; set; } = null!;
}
