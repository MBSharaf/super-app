using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>A national ID check (number stored encrypted). Table <c>auth.KycDocuments</c>.</summary>
public class KycDocument : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>Encrypted 14-digit national ID.</summary>
    public string NationalId { get; set; } = string.Empty;
    public long FrontFileId { get; set; }
    public long BackFileId { get; set; }
    public KycStatus Status { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public StoredFile FrontFile { get; set; } = null!;
    public StoredFile BackFile { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
