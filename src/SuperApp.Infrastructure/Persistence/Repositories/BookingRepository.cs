using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Bookings;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>
/// Bookings and holds. The database guarantees one booking per pitch, date and hour (unique index on active
/// slots) and one hold per hour, so two people pressing "book" at the same moment cannot both win.
/// </summary>
public sealed class BookingRepository(AppDbContext db, IClock clock) : IBookingRepository
{
    public static readonly TimeSpan HoldTime = TimeSpan.FromMinutes(10);
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Task<BookablePitch?> PitchAsync(string slug, CancellationToken ct) =>
        db.Set<Pitch>()
            .Where(p => p.Slug == slug && p.Status == PitchStatus.Live && p.Venue.Partner.Status == PartnerStatus.Active)
            .Select(p => new BookablePitch(p.Id, p.Slug, p.Name, p.Venue.Address, p.Venue.Partner.UserId, p.AllowCash, p.CancellationPolicy.Key,
                p.CancellationPolicy.FullRefundHours, p.CancellationPolicy.HalfRefundHours, p.Venue.Partner.BookingCommission))
            .FirstOrDefaultAsync(ct);

    public async Task<DateTime> HoldAsync(long pitchId, IReadOnlyList<(DateOnly Date, int Hour)> slots, Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await db.Set<SlotHold>().Where(h => h.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        var dates = slots.Select(s => s.Date).Distinct().ToList();
        var existing = await db.Set<SlotHold>().Where(h => h.PitchId == pitchId && dates.Contains(h.Date)).ToListAsync(ct);
        var mine = existing.Where(h => slots.Contains((h.Date, h.Hour))).ToList();
        if (mine.Any(h => h.UserId != userId))
        {
            throw new ConflictException("Someone is booking this hour right now. Try another hour or check again in a few minutes.", "slot_held");
        }
        db.Set<SlotHold>().RemoveRange(mine);
        var expires = now.Add(HoldTime);
        db.Set<SlotHold>().AddRange(slots.Select(s => new SlotHold { PitchId = pitchId, Date = s.Date, Hour = (byte)s.Hour, UserId = userId, ExpiresAt = expires }));
        await SaveOrConflictAsync("Someone is booking this hour right now.", "slot_held", ct);
        return expires;
    }

    public async Task SaveAsync(Booking booking, IReadOnlyList<(DateOnly Date, int Hour)> slots, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var dates = slots.Select(s => s.Date).Distinct().ToList();
        var holds = await db.Set<SlotHold>().Where(h => h.PitchId == booking.PitchId && dates.Contains(h.Date) && h.ExpiresAt > now).ToListAsync(ct);
        var onMine = holds.Where(h => slots.Contains((h.Date, h.Hour))).ToList();
        if (onMine.Any(h => h.UserId != booking.CustomerId))
        {
            throw new ConflictException("Someone is booking this hour right now. Please choose another one.", "slot_held");
        }
        db.Set<SlotHold>().RemoveRange(onMine);
        db.Set<Booking>().Add(booking);
        foreach (var s in slots)
        {
            db.Set<BookingSlot>().Add(new BookingSlot { Booking = booking, PitchId = booking.PitchId, Date = s.Date, Hour = (byte)s.Hour, IsActive = true });
        }
        await SaveOrConflictAsync("Someone just booked this hour. Please choose another one.", "slot_taken", ct);
    }

    public Task<Booking?> RecentSameAsync(Guid userId, long pitchId, DateOnly date, int startHour, int duration, CancellationToken ct)
    {
        var since = clock.UtcNow.AddMinutes(-10);
        return db.Set<Booking>().FirstOrDefaultAsync(b => b.CustomerId == userId && b.PitchId == pitchId && b.Date == date && b.StartHour == startHour
            && b.Duration == duration && b.Status == BookingStatus.Confirmed && b.CreatedAt >= since, ct);
    }

    public Task<Booking?> FindAsync(string code, CancellationToken ct) => db.Set<Booking>().FirstOrDefaultAsync(b => b.Code == code, ct);

    public async Task<PagedResult<Booking>> MineAsync(Guid userId, string tab, DateOnly today, int page, int pageSize, CancellationToken ct)
    {
        var mine = db.Set<Booking>().Where(b => b.CustomerId == userId);
        var cancelled = new[] { BookingStatus.CancelledByCustomer, BookingStatus.CancelledByVenue };
        var query = tab switch
        {
            "cancelled" => mine.Where(b => cancelled.Contains(b.Status)).OrderByDescending(b => b.CancelledAt),
            "past" => mine.Where(b => !cancelled.Contains(b.Status) && b.Date < today).OrderByDescending(b => b.Date).ThenByDescending(b => b.StartHour),
            _ => mine.Where(b => !cancelled.Contains(b.Status) && b.Date >= today).OrderBy(b => b.Date).ThenBy(b => b.StartHour),
        };
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync(ct);
        return new PagedResult<Booking>(items, page, pageSize, total);
    }

    public Task<Booking?> ByPaymentAsync(long paymentId, CancellationToken ct) => db.Set<Booking>().FirstOrDefaultAsync(b => b.PaymentId == paymentId, ct);

    public Task ReleaseSlotsAsync(long bookingId, CancellationToken ct) =>
        db.Set<BookingSlot>().Where(s => s.BookingId == bookingId).ExecuteUpdateAsync(u => u.SetProperty(s => s.IsActive, false), ct);

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "SA-" + new string(Enumerable.Range(0, 8).Select(_ => CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]).ToArray());
            if (!await db.Set<Booking>().AnyAsync(b => b.Code == code, ct))
            {
                return code;
            }
        }
    }

    public async Task<(string Slug, string Name, string Address)> PitchInfoAsync(long pitchId, CancellationToken ct)
    {
        var p = await db.Set<Pitch>().IgnoreQueryFilters().Where(x => x.Id == pitchId)
            .Select(x => new { x.Slug, x.Name, x.Venue.Address }).SingleAsync(ct);
        return (p.Slug, p.Name, p.Address);
    }

    public Task<string?> PackageCodeAsync(long packageId, CancellationToken ct) =>
        db.Set<Package>().Where(p => p.Id == packageId).Select(p => p.Code).FirstOrDefaultAsync(ct);

    /// <summary>A unique-index clash (2601 / 2627) means someone else got the hour first.</summary>
    private async Task SaveOrConflictAsync(string message, string code, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new ConflictException(message, code);
        }
    }
}
