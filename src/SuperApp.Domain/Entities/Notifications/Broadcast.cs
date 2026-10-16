using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Notifications;

/// <summary>A message sent to a segment of users. Table <c>notify.Broadcasts</c>.</summary>
public class Broadcast : BaseEntity, IHasAuthor
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Link { get; set; }
    public BroadcastSegment Segment { get; set; }
    public bool SendEmail { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    public int RecipientsCount { get; set; }
    public Guid? CreatedById { get; set; }
}
