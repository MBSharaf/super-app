using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A customer's acceptance of a legal version. Table <c>content.LegalAcceptances</c>.</summary>
public class LegalAcceptance : Entity<long>
{
    public long VersionId { get; set; }
    public Guid UserId { get; set; }
    public string Context { get; set; } = string.Empty;
    public long? ContextId { get; set; }
    public string? Ip { get; set; }
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public LegalVersion Version { get; set; } = null!;
    public User User { get; set; } = null!;
}
