using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.FreeSlots;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Free slots (one live slot per pitch and hour — a unique index) and the alerts that follow them.</summary>
public sealed class FreeSlotRepository(AppDbContext db) : IFreeSlotRepository
{
    public Task<int> PostedTodayAsync(long partnerId, DateTime sinceUtc, CancellationToken ct) =>
        db.Set<FreeSlot>().CountAsync(s => s.Pitch.Venue.PartnerId == partnerId && s.CreatedAt >= sinceUtc, ct);

    public Task<bool> PostingBlockedAsync(long partnerId, CancellationToken ct) =>
        db.Set<Partner>().AnyAsync(p => p.Id == partnerId && p.SlotPostingBlocked, ct);

    public Task<bool> LiveExistsAsync(long pitchId, DateOnly date, int hour, CancellationToken ct) =>
        db.Set<FreeSlot>().AnyAsync(s => s.PitchId == pitchId && s.Date == date && s.Hour == hour && s.Status == FreeSlotStatus.Live, ct);

    public void Add(FreeSlot slot, Offer offer)
    {
        db.Set<Offer>().Add(offer);
        db.Set<FreeSlot>().Add(slot);
    }

    public Task<FreeSlot?> PartnerSlotAsync(long partnerId, long id, CancellationToken ct) =>
        db.Set<FreeSlot>().Include(s => s.Offer).FirstOrDefaultAsync(s => s.Id == id && s.Pitch.Venue.PartnerId == partnerId && s.Status == FreeSlotStatus.Live, ct);

    public async Task<IReadOnlyList<FreeSlotDto>> LiveAsync(FreeSlotsQuery query, DateOnly today, long? partnerId, CancellationToken ct)
    {
        var q = db.Set<FreeSlot>().Where(s => s.Status == FreeSlotStatus.Live && s.Date >= today && s.Pitch.Status == PitchStatus.Live
            // An hour booked after it was posted is no longer free
            && !db.Set<BookingSlot>().Any(b => b.PitchId == s.PitchId && b.Date == s.Date && b.Hour == s.Hour && b.IsActive));
        if (partnerId is { } pid) { q = q.Where(s => s.Pitch.Venue.PartnerId == pid); }
        if (query.AreaId is { } area) { q = q.Where(s => s.Pitch.Venue.AreaId == area); }
        if (query.From is { } from) { q = q.Where(s => s.Date >= from); }
        if (query.To is { } to) { q = q.Where(s => s.Date <= to); }
        return await Rows(q.OrderBy(s => s.Date).ThenBy(s => s.Hour).Take(100)).ToListAsync(ct);
    }

    public Task<FreeSlotDto?> GetAsync(long id, CancellationToken ct) => Rows(db.Set<FreeSlot>().Where(s => s.Id == id)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> FollowersAsync(long pitchId, int hour, CancellationToken ct)
    {
        var area = await PitchAreaAsync(pitchId, ct);
        return await db.Set<SlotAlert>().Where(a => a.IsActive && (a.PitchId == pitchId || (a.PitchId == null && a.AreaId == area))
                && (a.FromHour == null || (hour >= a.FromHour && hour < a.ToHour)))
            .Select(a => a.UserId).Distinct().ToListAsync(ct);
    }

    public Task<int?> PitchAreaAsync(long pitchId, CancellationToken ct) =>
        db.Set<Pitch>().Where(p => p.Id == pitchId).Select(p => (int?)p.Venue.AreaId).FirstOrDefaultAsync(ct);

    public Task<bool> AreaExistsAsync(int areaId, CancellationToken ct) => db.Set<Area>().AnyAsync(a => a.Id == areaId, ct);

    public void AddAlert(SlotAlert alert) => db.Set<SlotAlert>().Add(alert);

    public async Task<IReadOnlyList<AlertDto>> AlertsAsync(Guid userId, CancellationToken ct) =>
        await db.Set<SlotAlert>().Where(a => a.UserId == userId && a.IsActive).OrderByDescending(a => a.CreatedAt)
            .Select(a => new AlertDto(a.Id, a.Pitch != null ? a.Pitch.Slug : null, a.Pitch != null ? a.Pitch.Name : null, a.AreaId, a.Area != null ? a.Area.NameEn : null,
                a.FromHour, a.ToHour, a.CreatedAt))
            .ToListAsync(ct);

    public Task<SlotAlert?> AlertAsync(Guid userId, long id, CancellationToken ct) => db.Set<SlotAlert>().FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

    public void RemoveAlert(SlotAlert alert) => db.Set<SlotAlert>().Remove(alert);

    /// <summary>Card fields; the grid day is the calendar day before for hours after midnight (offer date).</summary>
    private static IQueryable<FreeSlotDto> Rows(IQueryable<FreeSlot> q) => q.Select(s => new FreeSlotDto(
        s.Id, s.Pitch.Slug, s.Pitch.Name, s.Pitch.Venue.Area.NameEn, s.Pitch.Venue.AreaId,
        s.Offer != null ? s.Offer.StartsOn : s.Date, s.Offer != null && s.Offer.StartsOn != s.Date ? s.Hour + 24 : s.Hour, s.DiscountPercent,
        Math.Round((s.Hour >= 18 || s.Hour < 6 ? s.Pitch.PeakPricePerHour ?? s.Pitch.PricePerHour : s.Pitch.PricePerHour) * (100 - s.DiscountPercent) / 100, 0),
        s.Hour >= 18 || s.Hour < 6 ? s.Pitch.PeakPricePerHour ?? s.Pitch.PricePerHour : s.Pitch.PricePerHour));
}
