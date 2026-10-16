using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Support;

/// <summary>A support ticket with its live chat (and disputes). Table <c>support.Tickets</c>.</summary>
public class Ticket : BaseEntity, IHasRowVersion
{
    public string Code { get; set; } = string.Empty;
    public Guid OpenedById { get; set; }
    public TicketOrigin OpenedFrom { get; set; }
    public TicketTopic Topic { get; set; }
    public string Subject { get; set; } = string.Empty;
    public long? BookingId { get; set; }
    public long? OrderId { get; set; }
    public Guid? AgainstUserId { get; set; }
    public bool IsDispute { get; set; }
    public decimal? DisputeAmount { get; set; }
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public TicketStatus Status { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public long? AssignedToAgentId { get; set; }
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public User OpenedBy { get; set; } = null!;
    public Booking? Booking { get; set; }
    public Order? Order { get; set; }
    public User? AgainstUser { get; set; }
    public User? AssignedToUser { get; set; }
    public Agent? AssignedToAgent { get; set; }
    public ICollection<TicketMessage> Messages { get; set; } = [];
}
