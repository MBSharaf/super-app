using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>An option of a poll. Table <c>community.PollOptions</c>.</summary>
public class PollOption : Entity<long>
{
    public long PostId { get; set; }
    public string Text { get; set; } = string.Empty;
    public byte SortOrder { get; set; }
    public int VotesCount { get; set; }

    // Navigation
    public Post Post { get; set; } = null!;
    public ICollection<PollVote> Votes { get; set; } = [];
}
