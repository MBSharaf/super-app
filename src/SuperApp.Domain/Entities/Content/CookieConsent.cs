using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A visitor's cookie choice. Table <c>content.CookieConsents</c>.</summary>
public class CookieConsent : Entity<long>
{
    public string VisitorId { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public bool Analytics { get; set; }
    public short PolicyVersion { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}
