using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.AdminConsole;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Admin;

/// <summary>Orders, bookings, audit log and the overview.</summary>
public sealed partial class AdminReader
{
    public async Task<PagedResult<OrderRowDto>> OrdersAsync(OrdersQuery query, CancellationToken ct)
    {
        var orders = db.Set<Order>().AsQueryable();
        if (Enum.TryParse<OrderStatus>(query.Status, true, out var status))
        {
            orders = orders.Where(o => o.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            orders = orders.Where(o => o.Code.Contains(q) || o.Customer.FullName.Contains(q) || o.Phone.Contains(q));
        }
        if (query.From is { } from)
        {
            orders = orders.Where(o => o.CreatedAt >= from.ToDateTime(TimeOnly.MinValue));
        }
        if (query.To is { } to)
        {
            orders = orders.Where(o => o.CreatedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        }
        return await PageAsync(OrderRows(orders.OrderByDescending(o => o.CreatedAt)), query.Page, query.PageSize, ct);
    }

    public async Task<PagedResult<BookingRowDto>> BookingsAsync(OrdersQuery query, CancellationToken ct)
    {
        // Every booking, also at a pitch or venue removed since then (bookings are never soft-deleted)
        var bookings = db.Set<Booking>().IgnoreQueryFilters();
        if (Enum.TryParse<BookingStatus>(query.Status, true, out var status))
        {
            bookings = bookings.Where(b => b.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            bookings = bookings.Where(b => b.Code.Contains(q) || b.Pitch.Name.Contains(q) || (b.Customer != null && b.Customer.FullName.Contains(q)));
        }
        if (query.From is { } from)
        {
            bookings = bookings.Where(b => b.Date >= from);
        }
        if (query.To is { } to)
        {
            bookings = bookings.Where(b => b.Date <= to);
        }
        return await PageAsync(BookingRows(bookings.OrderByDescending(b => b.Date).ThenByDescending(b => b.StartHour)), query.Page, query.PageSize, ct);
    }

    public async Task<PagedResult<AuditRowDto>> AuditAsync(AuditQuery query, CancellationToken ct) =>
        await PageAsync(AuditRows(FilterAudit(query)), query.Page, query.PageSize, ct);

    internal IQueryable<AuditLog> FilterAudit(AuditQuery query)
    {
        var logs = db.Set<AuditLog>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Actor))
        {
            var a = query.Actor.Trim();
            logs = logs.Where(l => (l.ActorUser != null && (l.ActorUser.FullName.Contains(a) || l.ActorUser.Email!.Contains(a))) || (l.ActorAgent != null && l.ActorAgent.Name.Contains(a)));
        }
        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            logs = logs.Where(l => l.Action.StartsWith(query.Action.Trim()));
        }
        if (!string.IsNullOrWhiteSpace(query.Entity))
        {
            logs = logs.Where(l => l.EntityType == query.Entity.Trim());
        }
        if (Enum.TryParse<AuditSystem>(query.System, true, out var system))
        {
            logs = logs.Where(l => l.System == system);
        }
        if (query.From is { } from)
        {
            logs = logs.Where(l => l.At >= from.ToDateTime(TimeOnly.MinValue));
        }
        if (query.To is { } to)
        {
            logs = logs.Where(l => l.At < to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        }
        return logs.OrderByDescending(l => l.Id);
    }

    internal static IQueryable<AuditRowDto> AuditRows(IQueryable<AuditLog> logs) => logs.AsNoTracking().Select(l => new AuditRowDto(
        l.Id, l.At, l.System.ToString(), l.Action, l.ActorUser != null ? l.ActorUser.FullName : null, l.ActorAgent != null ? l.ActorAgent.Name : null,
        l.EntityType, l.EntityId, l.Summary, l.Ip));

    public async Task<AdminOverviewDto> OverviewAsync(DateTime now, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(now);
        var open = new[] { TicketStatus.Open, TicketStatus.WaitingForTeam, TicketStatus.WaitingForCustomer };
        var latest = await AuditRows(db.Set<AuditLog>().Where(l => l.Action != AiPanelReader.AgentCallAction).OrderByDescending(l => l.Id)).Take(10).ToListAsync(ct);
        return new AdminOverviewDto(
            await Customers().CountAsync(ct),
            await Customers().CountAsync(u => u.CreatedAt >= now.AddDays(-7), ct),
            await db.Set<Partner>().CountAsync(ct),
            await db.Set<Seller>().CountAsync(ct),
            await db.Set<Booking>().CountAsync(b => b.Date == today, ct),
            await db.Set<Order>().CountAsync(o => o.CreatedAt >= today.ToDateTime(TimeOnly.MinValue), ct),
            await db.Set<Ticket>().CountAsync(t => open.Contains(t.Status), ct),
            await db.Set<Ticket>().CountAsync(t => open.Contains(t.Status) && t.Priority == TicketPriority.Urgent, ct),
            await db.Set<PartnerApplication>().CountAsync(a => a.Status == ApplicationStatus.Pending, ct),
            await db.Set<ContentReport>().CountAsync(r => r.Status == ContentReportStatus.Open, ct),
            await db.Set<Agent>().CountAsync(a => a.Status == AgentStatus.Active, ct),
            latest);
    }
}

/// <summary>Tracked rows for the admin actions.</summary>
public sealed class AdminPeopleRepository(AppDbContext db, AdminReader reader) : IAdminPeopleRepository
{
    public Task<User?> CustomerAsync(Guid id, CancellationToken ct) => reader.Customers().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<Partner?> PartnerAsync(long id, CancellationToken ct) => db.Set<Partner>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Seller?> SellerAsync(long id, CancellationToken ct) => db.Set<Seller>().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Pitch?> PitchAsync(long id, CancellationToken ct) => db.Set<Pitch>().Include(p => p.Venue).ThenInclude(v => v.Partner).FirstOrDefaultAsync(p => p.Id == id, ct);
}
