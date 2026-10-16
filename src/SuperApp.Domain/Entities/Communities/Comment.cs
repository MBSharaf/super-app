using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>A comment or a reply. Table <c>community.Comments</c>.</summary>
public class Comment : BaseEntity, ISoftDelete
{
    public long PostId { get; set; }
    public long? ParentCommentId { get; set; }
    public Guid AuthorId { get; set; }
    public bool AsSuperAppAdmin { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public Post Post { get; set; } = null!;
    public Comment? ParentComment { get; set; }
    public User Author { get; set; } = null!;
}
