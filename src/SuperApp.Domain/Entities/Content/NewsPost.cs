using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Domain.Entities.Content;

/// <summary>
/// A news story written by the team (with a link to its source). <see cref="ClubId"/> tells that club's followers;
/// <see cref="Important"/> tells everyone. Table <c>content.NewsPosts</c>.
/// </summary>
public class NewsPost : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Language { get; set; } = "ar";
    public string? SourceName { get; set; }
    public string? SourceUrl { get; set; }
    public long? ClubId { get; set; }
    public bool Important { get; set; }
    public bool IsPublished { get; set; } = true;
    public DateTime PublishedAt { get; set; }
    public Guid AuthorId { get; set; }

    // Navigation
    public Club? Club { get; set; }
}
