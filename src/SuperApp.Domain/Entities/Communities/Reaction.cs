using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>A reaction on a post or a comment. Table <c>community.Reactions</c>.</summary>
public class Reaction : BaseEntity
{
    public Guid UserId { get; set; }
    public long? PostId { get; set; }
    public long? CommentId { get; set; }
    public ReactionType Type { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Post? Post { get; set; }
    public Comment? Comment { get; set; }
}
