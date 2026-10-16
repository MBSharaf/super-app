using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>A community post (text, photo, poll). Table <c>community.Posts</c>.</summary>
public class Post : BaseEntity, ISoftDelete
{
    public long CommunityId { get; set; }
    public Guid AuthorId { get; set; }
    public bool AsSuperAppAdmin { get; set; }
    public string Text { get; set; } = string.Empty;
    public long? ImageFileId { get; set; }
    public bool HasPoll { get; set; }
    public bool IsPinned { get; set; }
    public bool IsAnnouncement { get; set; }
    public bool CommentsClosed { get; set; }
    public bool IsHidden { get; set; }
    public int CommentsCount { get; set; }
    public int ReactionsCount { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public Community Community { get; set; } = null!;
    public User Author { get; set; } = null!;
    public StoredFile? ImageFile { get; set; }
    public ICollection<PollOption> PollOptions { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
}
