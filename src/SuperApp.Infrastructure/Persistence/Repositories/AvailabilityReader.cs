using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Reads what the hour grid needs for one live pitch and a date range, in a few small queries.</summary>
public sealed class AvailabilityReader(AppDbContext db) : IAvailabilityReader
{
    public async Task<AvailabilityData?> ReadAsync(string slug, DateOnly from, DateOnly to, DateTime nowUtc, CancellationToken ct)
    {
        var pitch = await db.Set<Pitch>()
            .Where(p => p.Slug == slug && p.Status == PitchStatus.Live && p.Venue.Partner.Status == PartnerStatus.Active)
            .Select(p => new { p.Id, p.PricePerHour, p.PeakPricePerHour, p.PeakFromHour })
            .FirstOrDefaultAsync(ct);
        if (pitch is null)
        {
            return null;
        }
        var id = pitch.Id;
        var hours = await db.Set<OpeningHour>().Where(h => h.PitchId == id)
            .Select(h => new OpeningHourDto(h.DayOfWeek, h.OpenHour, h.CloseHour, h.IsClosed)).ToListAsync(ct);
        var booked = await db.Set<BookingSlot>().Where(s => s.PitchId == id && s.IsActive && s.Date >= from && s.Date <= to)
            .Select(s => new { s.Date, s.Hour }).ToListAsync(ct);
        var held = await db.Set<SlotHold>().Where(s => s.PitchId == id && s.ExpiresAt > nowUtc && s.Date >= from && s.Date <= to)
            .Select(s => new { s.Date, s.Hour }).ToListAsync(ct);
        var closures = await db.Set<Closure>().Where(c => c.PitchId == id && c.FromDate <= to && c.ToDate >= from)
            .Select(c => new ClosureData(c.FromDate, c.ToDate, c.FromHour, c.ToHour)).ToListAsync(ct);
        var offers = await db.Set<Offer>().Where(o => o.PitchId == id && o.IsActive && o.StartsOn <= to && o.EndsOn >= from)
            .Select(o => new OfferData(o.DaysMask, o.FromHour, o.ToHour, o.DiscountPercent, o.StartsOn, o.EndsOn)).ToListAsync(ct);
        var holidays = await db.Set<PublicHoliday>().Where(h => h.Date >= from && h.Date <= to && h.IsDayOff)
            .Select(h => new { h.Date, h.NameEn, h.NameAr, h.IsEstimated }).ToListAsync(ct);
        var waiting = await db.Set<WaitlistEntry>().Where(w => w.PitchId == id && w.Status == WaitlistStatus.Waiting && w.Date >= from && w.Date <= to)
            .GroupBy(w => new { w.Date, w.Hour }).Select(g => new { g.Key.Date, g.Key.Hour, Count = g.Count() }).ToListAsync(ct);

        return new AvailabilityData(
            id, pitch.PricePerHour, pitch.PeakPricePerHour, pitch.PeakFromHour, hours,
            booked.Select(s => (s.Date, (int)s.Hour)).ToHashSet(),
            held.Select(s => (s.Date, (int)s.Hour)).ToHashSet(),
            closures, offers,
            holidays.GroupBy(h => h.Date).ToDictionary(g => g.Key, g => new HolidayBadgeDto(g.First().NameEn, g.First().NameAr, g.First().IsEstimated)),
            waiting.ToDictionary(w => (w.Date, (int)w.Hour), w => w.Count));
    }
}
