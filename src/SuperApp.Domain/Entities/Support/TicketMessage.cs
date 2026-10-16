using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Support;

/// <summary>A chat message in a ticket. Table <c>support.TicketMessages</c>.</summary>
public class TicketMessage : BaseEntity
{
    public long TicketId { get; set; }
    public Guid SenderId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsInternalNote { get; set; }
    public DateTime? ReadAt { get; set; }

    // Navigation
    public Ticket Ticket { get; set; } = null!;
    public User Sender { get; set; } = null!;
    public ICollection<TicketAttachment> Attachments { get; set; } = [];
}
