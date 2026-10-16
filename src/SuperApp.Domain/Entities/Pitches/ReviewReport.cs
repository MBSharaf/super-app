using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>An owner's report of a review. Table <c>pitch.ReviewReports</c>.</summary>
public class ReviewReport : BaseEntity
{
    public long ReviewId { get; set; }
    public Guid ReporterId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public ReviewReportStatus Status { get; set; }

    // Navigation
    public Review Review { get; set; } = null!;
    public User Reporter { get; set; } = null!;
}
