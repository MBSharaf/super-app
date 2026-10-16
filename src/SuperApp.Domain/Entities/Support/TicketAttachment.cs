using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Domain.Entities.Support;

/// <summary>A file attached to a ticket message. Table <c>support.TicketAttachments</c>.</summary>
public class TicketAttachment : Entity<long>
{
    public long MessageId { get; set; }
    public long FileId { get; set; }

    // Navigation
    public TicketMessage Message { get; set; } = null!;
    public StoredFile File { get; set; } = null!;
}
