using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A version of a legal document (Arabic and English). Table <c>content.LegalVersions</c>.</summary>
public class LegalVersion : BaseEntity
{
    public int DocumentId { get; set; }
    public short Version { get; set; }
    public string BodyEn { get; set; } = string.Empty;
    public string BodyAr { get; set; } = string.Empty;
    public string SummaryEn { get; set; } = string.Empty;
    public string SummaryAr { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public DateTime? LawyerReviewedAt { get; set; }
    public string? LawyerNote { get; set; }
    public Guid? ReviewedById { get; set; }

    // Navigation
    public LegalDocument Document { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
