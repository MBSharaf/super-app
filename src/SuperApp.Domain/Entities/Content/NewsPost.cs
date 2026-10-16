using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A Super App news story. Table <c>content.NewsPosts</c>.</summary>
public class NewsPost : BaseEntity, ISoftDelete
{
    public Guid AuthorId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public long? ImageFileId { get; set; }
    public bool IsBreaking { get; set; }
    public bool IsHidden { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public User Author { get; set; } = null!;
    public StoredFile? ImageFile { get; set; }
}
