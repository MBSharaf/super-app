using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>A request to become a pitch owner, a seller or a factory. Table <c>auth.Applications</c>.</summary>
public class PartnerApplication : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public Guid ApplicantId { get; set; }
    public ApplicationType Type { get; set; }
    public string AnswersJson { get; set; } = string.Empty;
    public long AgreementVersionId { get; set; }
    public ApplicationStatus Status { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }

    // Navigation
    public User Applicant { get; set; } = null!;
    public LegalVersion AgreementVersion { get; set; } = null!;
    public User? ReviewedBy { get; set; }
    public ICollection<ApplicationFile> Files { get; set; } = [];
}
