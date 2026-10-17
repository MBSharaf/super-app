using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.CheckIn;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>QR scans and the bookings they check in. The unique Idempotency-Key index stops a scan being stored twice.</summary>
public sealed class CheckInRepository(AppDbContext db) : ICheckInRepository
{
    public async Task<(QrScan Scan, string? Code)?> ByKeyAsync(string idempotencyKey, CancellationToken ct)
    {
        var row = await db.Set<QrScan>().AsNoTracking().Where(s => s.IdempotencyKey == idempotencyKey)
            .Select(s => new { Scan = s, Code = s.Booking != null ? s.Booking.Code : null }).FirstOrDefaultAsync(ct);
        return row is null ? null : (row.Scan, row.Code);
    }

    public async Task<ScanTarget?> BookingAsync(string code, CancellationToken ct)
    {
        var booking = await db.Set<Booking>().FirstOrDefaultAsync(b => b.Code == code, ct);
        if (booking is null)
        {
            return null;
        }
        var info = await db.Set<Booking>().Where(b => b.Id == booking.Id)
            .Select(b => new
            {
                b.Pitch.Venue.PartnerId, PitchName = b.Pitch.Name,
                Customer = b.Customer != null ? b.Customer.FullName : b.WalkInName ?? "Walk-in",
                Blocked = b.Customer != null && b.Customer.Status != UserStatus.Active,
            })
            .IgnoreQueryFilters().SingleAsync(ct);
        return new ScanTarget(booking, info.PartnerId, info.PitchName, info.Customer, info.Blocked);
    }

    public void Add(QrScan scan) => db.Set<QrScan>().Add(scan);

    public async Task<IReadOnlyList<ScanRowDto>> HistoryAsync(long pitchId, DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        await db.Set<QrScan>().Where(s => s.PitchId == pitchId && s.ScannedAt >= fromUtc && s.ScannedAt < toUtc).OrderByDescending(s => s.ScannedAt)
            .Select(s => new ScanRowDto(s.Id, s.ScannedAt, s.Result.ToString(), s.Booking != null ? s.Booking.Code : s.Package != null ? s.Package.Code : null,
                s.Booking != null ? (s.Booking.Customer != null ? s.Booking.Customer.FullName : s.Booking.WalkInName) : s.Package != null ? s.Package.Customer.FullName : null,
                s.ScannedBy.FullName, s.MatchNumber, s.Device))
            .ToListAsync(ct);
}
