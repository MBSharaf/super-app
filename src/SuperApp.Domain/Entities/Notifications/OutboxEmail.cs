using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Notifications;

/// <summary>An email waiting to be sent (or sent). Table <c>notify.EmailOutbox</c>.</summary>
public class OutboxEmail : Entity<long>
{
    public Guid? UserId { get; set; }
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Template { get; set; } = string.Empty;
    public string ModelJson { get; set; } = string.Empty;
    public string Language { get; set; } = "ar";
    public EmailStatus Status { get; set; }
    public byte Attempts { get; set; }
    public DateTime? SentAt { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public User? User { get; set; }
}
