using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>A vote on a poll option (one per poll). Table <c>community.PollVotes</c>.</summary>
public class PollVote
{
    public long OptionId { get; set; }
    public Guid UserId { get; set; }
    public long PostId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public PollOption Option { get; set; } = null!;
    public User User { get; set; } = null!;
    public Post Post { get; set; } = null!;
}
