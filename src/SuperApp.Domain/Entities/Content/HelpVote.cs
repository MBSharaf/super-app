using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A 'was this helpful' vote. Table <c>content.HelpVotes</c>.</summary>
public class HelpVote : BaseEntity
{
    public long ArticleId { get; set; }
    public Guid? UserId { get; set; }
    public string? VisitorId { get; set; }
    public bool Helpful { get; set; }

    // Navigation
    public HelpArticle Article { get; set; } = null!;
    public User? User { get; set; }
}
