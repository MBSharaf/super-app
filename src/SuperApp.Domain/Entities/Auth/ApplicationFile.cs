using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>A document attached to an application. Table <c>auth.ApplicationFiles</c>.</summary>
public class ApplicationFile : BaseEntity
{
    public long ApplicationId { get; set; }
    public long FileId { get; set; }
    public ApplicationFilePurpose Purpose { get; set; }

    // Navigation
    public PartnerApplication Application { get; set; } = null!;
    public StoredFile File { get; set; } = null!;
}
