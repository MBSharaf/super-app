using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Partners;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Bookings on a partner's own pitches (search, export, desk actions).</summary>
public sealed class PartnerBookingsRepository(AppDbContext db) : IPartnerBookingsRepository
{
    private static readonly BookingStatus[] Cancelled = [BookingStatus.CancelledByCustomer, BookingStatus.CancelledByVenue];

    public async Task<PagedResult<PartnerBookingDto>> SearchAsync(long partnerId, PartnerBookingsQuery query, DateOnly today, CancellationToken ct)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var filtered = Filter(partnerId, query, today);
        var total = await filtered.CountAsync(ct);
        var items = await Order(filtered, query.Status).Skip(page.Skip).Take(page.SafePageSize).Select(Row).ToListAsync(ct);
        return new PagedResult<PartnerBookingDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<IReadOnlyList<PartnerBookingDto>> AllAsync(long partnerId, PartnerBookingsQuery query, DateOnly today, int max, CancellationToken ct) =>
        await Order(Filter(partnerId, query, today), query.Status).Take(max).Select(Row).ToListAsync(ct);

    public Task<PartnerBookingDto?> GetAsync(long partnerId, string code, CancellationToken ct) =>
        Mine(partnerId).Where(b => b.Code == code).Select(Row).FirstOrDefaultAsync(ct);

    public Task<Booking?> FindAsync(long partnerId, string code, CancellationToken ct) =>
        Mine(partnerId).FirstOrDefaultAsync(b => b.Code == code, ct);

    public async Task<(string Slug, string Name)?> PitchAsync(long partnerId, long pitchId, CancellationToken ct)
    {
        var p = await db.Set<Pitch>().Where(x => x.Id == pitchId && x.Venue.PartnerId == partnerId).Select(x => new { x.Slug, x.Name }).FirstOrDefaultAsync(ct);
        return p is null ? null : (p.Slug, p.Name);
    }

    private IQueryable<Booking> Mine(long partnerId) => db.Set<Booking>().Where(b => b.Pitch.Venue.PartnerId == partnerId);

    private IQueryable<Booking> Filter(long partnerId, PartnerBookingsQuery q, DateOnly today)
    {
        var query = Mine(partnerId).AsNoTracking();
        if (q.PitchId is { } pitchId) { query = query.Where(b => b.PitchId == pitchId); }
        if (q.From is { } from) { query = query.Where(b => b.Date >= from); }
        if (q.To is { } to) { query = query.Where(b => b.Date <= to); }
        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var text = q.Q.Trim();
            query = query.Where(b => b.Code.Contains(text) || (b.WalkInName != null && b.WalkInName.Contains(text)) || (b.WalkInPhone != null && b.WalkInPhone.Contains(text))
                || (b.Customer != null && (b.Customer.FullName.Contains(text) || (b.Customer.PhoneNumber != null && b.Customer.PhoneNumber.Contains(text)))));
        }
        return q.Status switch
        {
            "all" => query,
            "cancelled" => query.Where(b => Cancelled.Contains(b.Status)),
            "noshow" => query.Where(b => b.Status == BookingStatus.NoShow),
            "past" => query.Where(b => !Cancelled.Contains(b.Status) && b.Date < today),
            _ => query.Where(b => !Cancelled.Contains(b.Status) && b.Date >= today),
        };
    }

    private static IQueryable<Booking> Order(IQueryable<Booking> q, string? status) => status is "upcoming" or null
        ? q.OrderBy(b => b.Date).ThenBy(b => b.StartHour)
        : q.OrderByDescending(b => b.Date).ThenByDescending(b => b.StartHour);

    private static readonly System.Linq.Expressions.Expression<Func<Booking, PartnerBookingDto>> Row = b => new PartnerBookingDto(
        b.Code, b.PitchId, b.Pitch.Name, b.Date, b.StartHour, b.Duration,
        b.Customer != null ? b.Customer.FullName : b.WalkInName ?? "Walk-in",
        b.Customer != null ? b.Customer.PhoneNumber : b.WalkInPhone,
        b.Source.ToString(), b.PitchAmount, b.ServiceFee, b.Total, b.CommissionRate, b.PayAtVenue, b.Status.ToString(),
        b.CheckedInAt, b.CancelledAt, b.RefundAmount, b.CreatedAt);
}
