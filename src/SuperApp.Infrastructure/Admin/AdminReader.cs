using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.AdminConsole;
using SuperApp.Application.Security;
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

/// <summary>The admin console's lists and drawers. Customers are people who are not on the team (no CEO role, no admin permission).</summary>
public sealed partial class AdminReader(AppDbContext db) : IAdminReader
{
    internal IQueryable<User> Customers() => db.Users.Where(u => u.AccountType == AccountType.Person
        && !db.UserRoles.Any(ur => ur.UserId == u.Id && (ur.Role.Name == SystemRoles.Ceo || db.Set<RolePermission>().Any(rp => rp.RoleId == ur.RoleId))));

    public async Task<PagedResult<CustomerRowDto>> CustomersAsync(PeopleQuery query, CancellationToken ct)
    {
        var users = Customers();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            users = users.Where(u => u.FullName.Contains(q) || u.Email!.Contains(q) || u.PhoneNumber!.Contains(q));
        }
        if (Enum.TryParse<UserStatus>(query.Status, true, out var status))
        {
            users = users.Where(u => u.Status == status);
        }
        return await PageAsync(CustomerRows(users.OrderByDescending(u => u.CreatedAt)), query.Page, query.PageSize, ct);
    }

    public async Task<CustomerDetailDto?> CustomerAsync(Guid id, CancellationToken ct)
    {
        var row = await CustomerRows(Customers().Where(u => u.Id == id)).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }
        var extra = await db.Users.Where(u => u.Id == id).Select(u => new { u.AdminNote, u.SuspendedReason, u.Language }).FirstAsync(ct);
        var bookings = await BookingRows(db.Set<Booking>().Where(b => b.CustomerId == id).OrderByDescending(b => b.CreatedAt)).Take(10).ToListAsync(ct);
        var orders = await OrderRows(db.Set<Order>().Where(o => o.CustomerId == id).OrderByDescending(o => o.CreatedAt)).Take(10).ToListAsync(ct);
        var tickets = await db.Set<Ticket>().CountAsync(t => t.OpenedById == id && t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed, ct);
        return new CustomerDetailDto(row, extra.AdminNote, extra.SuspendedReason, extra.Language, bookings, orders, tickets);
    }

    public async Task<PagedResult<PartnerRowDto>> PartnersAsync(PeopleQuery query, DateTime now, CancellationToken ct)
    {
        var partners = db.Set<Partner>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            partners = partners.Where(p => p.BusinessName.Contains(q) || p.User.FullName.Contains(q) || p.User.Email!.Contains(q));
        }
        if (Enum.TryParse<PartnerStatus>(query.Status, true, out var status))
        {
            partners = partners.Where(p => p.Status == status);
        }
        return await PageAsync(PartnerRows(partners.OrderBy(p => p.BusinessName), now), query.Page, query.PageSize, ct);
    }

    public async Task<PartnerDetailDto?> PartnerAsync(long id, DateTime now, CancellationToken ct)
    {
        var row = await PartnerRows(db.Set<Partner>().Where(p => p.Id == id), now).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }
        var extra = await db.Set<Partner>().Where(p => p.Id == id).Select(p => new { p.SuspendedReason, p.SlotPostingBlocked }).FirstAsync(ct);
        var venues = await db.Set<Venue>().Where(v => v.PartnerId == id).OrderBy(v => v.Name)
            .Select(v => new VenueBriefDto(v.Id, v.Name, db.Set<Pitch>().Count(p => p.VenueId == v.Id))).ToListAsync(ct);
        return new PartnerDetailDto(row, extra.SuspendedReason, extra.SlotPostingBlocked, venues);
    }

    public async Task<PagedResult<SellerRowDto>> SellersAsync(PeopleQuery query, CancellationToken ct)
    {
        var sellers = db.Set<Seller>().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            var q = query.Q.Trim();
            sellers = sellers.Where(s => s.User.FullName.Contains(q) || s.Store.Name.Contains(q) || s.User.Email!.Contains(q));
        }
        if (Enum.TryParse<SellerStatus>(query.Status, true, out var status))
        {
            sellers = sellers.Where(s => s.Status == status);
        }
        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            sellers = sellers.Where(s => s.Level.NameEn == query.Level);
        }
        return await PageAsync(SellerRows(sellers.OrderByDescending(s => s.LifetimeSales)), query.Page, query.PageSize, ct);
    }

    public async Task<SellerDetailDto?> SellerAsync(long id, CancellationToken ct)
    {
        var row = await SellerRows(db.Set<Seller>().Where(s => s.Id == id)).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }
        var now = DateTime.UtcNow;
        var strikes = await db.Set<SellerStrike>().CountAsync(s => s.SellerId == id && s.RemovedAt == null && s.ExpiresAt > now, ct);
        var followers = await db.Set<SellerFollower>().CountAsync(f => f.SellerId == id, ct);
        return new SellerDetailDto(row, strikes, followers);
    }

    internal IQueryable<CustomerRowDto> CustomerRows(IQueryable<User> users) => users.AsNoTracking().Select(u => new CustomerRowDto(
        u.Id, u.FullName, u.Email, u.PhoneNumber, u.Status.ToString(), u.MustPrepay.ToString(), u.CodRefusals, u.CreatedAt, u.LastSeenAt,
        db.Set<Booking>().Count(b => b.CustomerId == u.Id), db.Set<Order>().Count(o => o.CustomerId == u.Id)));

    private IQueryable<PartnerRowDto> PartnerRows(IQueryable<Partner> partners, DateTime now)
    {
        var since = DateOnly.FromDateTime(now.AddDays(-30));
        return partners.AsNoTracking().Select(p => new PartnerRowDto(p.Id, p.UserId, p.BusinessName, p.User.FullName, p.User.Email, p.User.PhoneNumber,
            p.Status.ToString(), db.Set<Venue>().Count(v => v.PartnerId == p.Id), db.Set<Pitch>().Count(x => x.Venue.PartnerId == p.Id),
            db.Set<Booking>().Count(b => b.Pitch.Venue.PartnerId == p.Id && b.Date >= since), p.CreatedAt));
    }

    internal IQueryable<SellerRowDto> SellerRows(IQueryable<Seller> sellers) => sellers.AsNoTracking().Select(s => new SellerRowDto(
        s.Id, s.UserId, s.User.FullName, s.User.Email, s.Store.Name, s.Level.NameEn, s.Mode.ToString(), s.Status.ToString(), s.LifetimeSales, s.Rating,
        s.FeaturedByAdmin, s.FrozenUntil, db.Set<Design>().Count(d => d.SellerId == s.Id && d.Status == DesignStatus.Live), s.CreatedAt));

    internal static IQueryable<OrderRowDto> OrderRows(IQueryable<Order> orders) => orders.AsNoTracking().Select(o => new OrderRowDto(
        o.Code, o.Customer.FullName, o.Status.ToString(), o.PaymentMethod.ToString(), o.Items.Count, o.Total, o.CreatedAt));

    internal static IQueryable<BookingRowDto> BookingRows(IQueryable<Booking> bookings) => bookings.AsNoTracking().Select(b => new BookingRowDto(
        b.Code, b.Pitch.Name, b.Pitch.Venue.Name, b.Customer != null ? b.Customer.FullName : b.WalkInName, b.Date, b.StartHour, b.Duration,
        b.Status.ToString(), b.Source.ToString(), b.Total, b.CreatedAt));

    internal static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> rows, int page, int pageSize, CancellationToken ct)
    {
        var request = new PageRequest(page, pageSize);
        var total = await rows.CountAsync(ct);
        var items = await rows.Skip(request.Skip).Take(request.SafePageSize).ToListAsync(ct);
        return new PagedResult<T>(items, request.SafePage, request.SafePageSize, total);
    }
}
