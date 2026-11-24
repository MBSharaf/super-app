using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Notifications;

/// <summary>A phone that gets push notifications (Firebase Cloud Messaging token). Table <c>notify.PushDevices</c>.</summary>
public class PushDevice : Entity<long>
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = "android";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User User { get; set; } = null!;
}
