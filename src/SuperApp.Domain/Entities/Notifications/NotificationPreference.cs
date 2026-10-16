using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Notifications;

/// <summary>A user's choice per notification category. Table <c>notify.NotificationPreferences</c>.</summary>
public class NotificationPreference
{
    public Guid UserId { get; set; }
    public NotificationCategory Category { get; set; }
    public bool InApp { get; set; } = true;
    public bool Email { get; set; } = true;
    public bool Push { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
