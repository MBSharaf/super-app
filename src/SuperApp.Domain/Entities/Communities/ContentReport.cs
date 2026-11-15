using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>A report of a post or a comment. Table <c>community.ContentReports</c>.</summary>
public class ContentReport : BaseEntity
{
    public Guid ReporterId { get; set; }
    public long? PostId { get; set; }
    public long? CommentId { get; set; }
    public ContentReportReason Reason { get; set; }
    public string? Detail { get; set; }
    public ContentReportStatus Status { get; set; }
    public Guid? HandledById { get; set; }

    // Navigation
    public User Reporter { get; set; } = null!;
    public Post? Post { get; set; }
    public Comment? Comment { get; set; }
    public User? HandledBy { get; set; }
}
