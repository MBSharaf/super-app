using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Support;

namespace SuperApp.Application.Features.Support;

public sealed record AttachmentDto(long FileId, string Url);

/// <summary>A chat message. <c>FromTeam</c> for Super App replies; internal notes only reach the team.</summary>
public sealed record TicketMessageDto(long Id, string Sender, bool FromTeam, bool IsMine, string Body, bool IsInternalNote, DateTime CreatedAt, DateTime? ReadAt,
    IReadOnlyList<AttachmentDto> Attachments);

/// <summary>A ticket. Topic Booking / Order / Payment / Account / Fantasy / Seller / Partner / Other; status Open / WaitingForCustomer / WaitingForTeam / Resolved / Closed.</summary>
public sealed record TicketDto(string Code, string Subject, string Topic, string OpenedFrom, string Status, string Priority, string? BookingCode, string? OrderCode,
    bool IsDispute, decimal? DisputeAmount, string OpenedBy, string? AssignedTo, DateTime CreatedAt, DateTime? FirstResponseAt, DateTime? ResolvedAt,
    int Unread, IReadOnlyList<TicketMessageDto>? Messages);

/// <summary>POST /support/tickets — booking or order by its code (it must be yours); dispute with an amount.</summary>
public sealed record OpenTicketRequest(string Topic, string Subject, string Message, string? OpenedFrom, string? BookingCode, string? OrderCode, decimal? DisputeAmount);

public sealed record TicketMessageRequest(string Body);

public sealed record TeamMessageRequest(string Body, bool InternalNote);

public sealed record AssignRequest(Guid? UserId);

public sealed record PriorityRequest(string Priority);

/// <summary>A dispute decision: refund amount (0 = none) and who pays (SuperApp, Partner, Seller). The money moves in Phase 5.</summary>
public sealed record DisputeRequest(decimal Refund, string PaidBy, string Note);

public sealed record TicketsQuery(string? Status = null, string? Priority = null, Guid? Assignee = null, int Page = 1, int PageSize = 20);

/// <summary>The person a ticket belongs to, for emails.</summary>
public sealed record TicketOwner(Guid UserId, string Email, string Name, string Language);

public interface ISupportRepository
{
    Task<string> NewCodeAsync(CancellationToken ct);

    Task<long?> BookingIdAsync(string code, Guid customerId, CancellationToken ct);

    Task<long?> OrderIdAsync(string code, Guid customerId, CancellationToken ct);

    /// <summary>The other side of a dispute about a booking (pitch owner) or an order (store owner).</summary>
    Task<Guid?> CounterpartyAsync(long? bookingId, long? orderId, CancellationToken ct);

    void Add(Ticket ticket);

    void Add(TicketMessage message);

    void Add(TicketAttachment attachment);

    Task<Ticket?> TicketAsync(string code, CancellationToken ct);

    Task<TicketDto?> TicketDtoAsync(string code, Guid viewer, bool team, CancellationToken ct);

    Task<IReadOnlyList<TicketDto>> MineAsync(Guid userId, CancellationToken ct);

    Task<PagedResult<TicketDto>> InboxAsync(TicketsQuery query, CancellationToken ct);

    /// <summary>Marks the other side's messages as read.</summary>
    Task<int> MarkReadAsync(long ticketId, bool byTeam, DateTime at, CancellationToken ct);

    Task<TicketOwner> OwnerAsync(Guid userId, CancellationToken ct);

    Task<bool> IsStaffAsync(Guid userId, CancellationToken ct);

    /// <summary>True when the stored file is attached to a ticket message (the support team may open it).</summary>
    Task<bool> IsAttachmentAsync(long fileId, CancellationToken ct);
}

/// <summary>Live chat events on a ticket (SignalR group per ticket): <c>message</c>, <c>read</c>, <c>statusChanged</c>, <c>assigned</c>.</summary>
public interface ISupportLive
{
    Task PublishAsync(string ticketCode, string eventName, object payload, CancellationToken ct);
}
