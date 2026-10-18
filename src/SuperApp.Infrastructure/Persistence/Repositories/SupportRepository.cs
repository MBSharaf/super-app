using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Support;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Tickets, their chat messages and attachments.</summary>
public sealed class SupportRepository(AppDbContext db) : ISupportRepository
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "TK-" + new string(Enumerable.Range(0, 6).Select(_ => Letters[RandomNumberGenerator.GetInt32(Letters.Length)]).ToArray());
            if (!await db.Set<Ticket>().AnyAsync(t => t.Code == code, ct))
            {
                return code;
            }
        }
    }

    public Task<long?> BookingIdAsync(string code, Guid customerId, CancellationToken ct) =>
        db.Set<Booking>().Where(b => b.Code == code && b.CustomerId == customerId).Select(b => (long?)b.Id).FirstOrDefaultAsync(ct);

    public Task<long?> OrderIdAsync(string code, Guid customerId, CancellationToken ct) =>
        db.Set<Order>().Where(o => o.Code == code && o.CustomerId == customerId).Select(o => (long?)o.Id).FirstOrDefaultAsync(ct);

    public async Task<Guid?> CounterpartyAsync(long? bookingId, long? orderId, CancellationToken ct) => bookingId is null ? null
        : await db.Set<Booking>().Where(b => b.Id == bookingId).Select(b => (Guid?)b.Pitch.Venue.Partner.UserId).FirstOrDefaultAsync(ct);

    public void Add(Ticket ticket) => db.Set<Ticket>().Add(ticket);

    public void Add(TicketMessage message) => db.Set<TicketMessage>().Add(message);

    public void Add(TicketAttachment attachment) => db.Set<TicketAttachment>().Add(attachment);

    public Task<Ticket?> TicketAsync(string code, CancellationToken ct) => db.Set<Ticket>().FirstOrDefaultAsync(t => t.Code == code, ct);

    public async Task<TicketDto?> TicketDtoAsync(string code, Guid viewer, bool team, CancellationToken ct)
    {
        var ticket = await Project(db.Set<Ticket>().Where(t => t.Code == code), team).FirstOrDefaultAsync(ct);
        if (ticket is null)
        {
            return null;
        }
        var rows = await db.Set<TicketMessage>().AsNoTracking().Where(m => m.Ticket.Code == code && (team || !m.IsInternalNote)).OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new
            {
                m.Id, m.SenderId, Sender = m.Sender.FullName, FromTeam = m.SenderId != m.Ticket.OpenedById, m.Body, m.IsInternalNote, m.CreatedAt, m.ReadAt,
                Files = db.Set<TicketAttachment>().Where(a => a.MessageId == m.Id).Select(a => a.FileId).ToList(),
            }).ToListAsync(ct);
        var messages = rows.Select(m => new TicketMessageDto(m.Id, m.FromTeam ? (team ? m.Sender : "Super App") : m.Sender, m.FromTeam, m.SenderId == viewer, m.Body,
            m.IsInternalNote, m.CreatedAt, m.ReadAt, m.Files.Select(f => new AttachmentDto(f, $"/api/v1/files/{f}")).ToList())).ToList();
        // The auto-reply ("we reply within 24 hours") stands right after the first message (FR-SUP-04)
        messages.Insert(Math.Min(1, messages.Count), new TicketMessageDto(0, "Super App", true, false, SupportService.AutoReply, false, ticket.CreatedAt, null, []));
        return ticket with { Messages = messages };
    }

    public async Task<IReadOnlyList<TicketDto>> MineAsync(Guid userId, CancellationToken ct) =>
        await Project(db.Set<Ticket>().Where(t => t.OpenedById == userId).OrderByDescending(t => t.CreatedAt), false).ToListAsync(ct);

    public async Task<PagedResult<TicketDto>> InboxAsync(TicketsQuery query, CancellationToken ct)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var tickets = db.Set<Ticket>().AsQueryable();
        if (Enum.TryParse<TicketStatus>(query.Status, true, out var status))
        {
            tickets = tickets.Where(t => t.Status == status);
        }
        else
        {
            tickets = tickets.Where(t => t.Status != TicketStatus.Closed && t.Status != TicketStatus.Resolved);
        }
        if (Enum.TryParse<TicketPriority>(query.Priority, true, out var priority))
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }
        if (query.Assignee is { } assignee)
        {
            tickets = tickets.Where(t => t.AssignedToUserId == assignee);
        }
        var total = await tickets.CountAsync(ct);
        var items = await Project(tickets.OrderByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).Skip(page.Skip).Take(page.SafePageSize), true).ToListAsync(ct);
        return new PagedResult<TicketDto>(items, page.SafePage, page.SafePageSize, total);
    }

    /// <summary>Unread = the other side's messages not read yet (customers never count internal notes).</summary>
    private IQueryable<TicketDto> Project(IQueryable<Ticket> tickets, bool team) => tickets.AsNoTracking().Select(t => new TicketDto(t.Code, t.Subject, t.Topic.ToString(),
        t.OpenedFrom.ToString(), t.Status.ToString(), t.Priority.ToString(), t.Booking == null ? null : t.Booking.Code, t.Order == null ? null : t.Order.Code,
        t.IsDispute, t.DisputeAmount, t.OpenedBy.FullName, t.AssignedToUser == null ? null : t.AssignedToUser.FullName, t.CreatedAt, t.FirstResponseAt, t.ResolvedAt,
        db.Set<TicketMessage>().Count(m => m.TicketId == t.Id && m.ReadAt == null && !m.IsInternalNote && (team ? m.SenderId == t.OpenedById : m.SenderId != t.OpenedById)),
        null));

    public Task<int> MarkReadAsync(long ticketId, bool byTeam, DateTime at, CancellationToken ct) =>
        db.Set<TicketMessage>().Where(m => m.TicketId == ticketId && m.ReadAt == null && !m.IsInternalNote
                && (byTeam ? m.SenderId == m.Ticket.OpenedById : m.SenderId != m.Ticket.OpenedById))
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.ReadAt, at), ct);

    public Task<TicketOwner> OwnerAsync(Guid userId, CancellationToken ct) =>
        db.Users.Where(u => u.Id == userId).Select(u => new TicketOwner(u.Id, u.Email ?? string.Empty, u.FullName, u.Language)).SingleAsync(ct);

    /// <summary>A team member who can see tickets (role or direct grant of support.read, or the CEO).</summary>
    public Task<bool> IsStaffAsync(Guid userId, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active
            && (db.UserRoles.Any(ur => ur.UserId == u.Id && (ur.Role.Name == SystemRoles.Ceo || db.Set<RolePermission>().Any(rp => rp.RoleId == ur.RoleId && rp.Permission.Code == "support.read")))
                || db.Set<UserPermission>().Any(up => up.UserId == u.Id && up.Permission.Code == "support.read")), ct);
}
