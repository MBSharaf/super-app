using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Waitlist;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Waitlist entries (one per person, pitch, date and hour — a unique index).</summary>
public sealed class WaitlistRepository(AppDbContext db) : IWaitlistRepository
{
    public Task<WaitlistEntryDto?> GetAsync(long id, CancellationToken ct) => Rows(db.Set<WaitlistEntry>().Where(w => w.Id == id)).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(long pitchId, DateOnly date, int hour, Guid userId, CancellationToken ct) =>
        db.Set<WaitlistEntry>().AnyAsync(w => w.PitchId == pitchId && w.Date == date && w.Hour == hour && w.UserId == userId, ct);

    public void Add(WaitlistEntry entry) => db.Set<WaitlistEntry>().Add(entry);

    public async Task<IReadOnlyList<WaitlistEntryDto>> MineAsync(Guid userId, DateOnly from, CancellationToken ct) =>
        await Rows(db.Set<WaitlistEntry>().Where(w => w.UserId == userId && w.Date >= from && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.FreeNow))
            .OrderBy(w => w.Date).ThenBy(w => w.Hour)).ToListAsync(ct);

    public Task<WaitlistEntry?> MineAsync(Guid userId, long id, CancellationToken ct) =>
        db.Set<WaitlistEntry>().FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);

    public void Remove(WaitlistEntry entry) => db.Set<WaitlistEntry>().Remove(entry);

    public async Task<IReadOnlyList<WaitlistEntry>> WaitingForAsync(long pitchId, IReadOnlyList<(DateOnly Date, int Hour)> hours, CancellationToken ct)
    {
        var dates = hours.Select(h => h.Date).Distinct().ToList();
        var rows = await db.Set<WaitlistEntry>().Where(w => w.PitchId == pitchId && dates.Contains(w.Date) && w.Status == WaitlistStatus.Waiting).ToListAsync(ct);
        return rows.Where(w => hours.Contains((w.Date, w.Hour))).ToList();
    }

    public async Task<IReadOnlyList<WaitlistEntry>> OwnForAsync(long pitchId, Guid userId, IReadOnlyList<(DateOnly Date, int Hour)> hours, CancellationToken ct)
    {
        var dates = hours.Select(h => h.Date).Distinct().ToList();
        var rows = await db.Set<WaitlistEntry>().Where(w => w.PitchId == pitchId && w.UserId == userId && dates.Contains(w.Date)
            && (w.Status == WaitlistStatus.Waiting || w.Status == WaitlistStatus.FreeNow)).ToListAsync(ct);
        return rows.Where(w => hours.Contains((w.Date, w.Hour))).ToList();
    }

    private IQueryable<WaitlistEntryDto> Rows(IQueryable<WaitlistEntry> q) => q.Select(w => new WaitlistEntryDto(
        w.Id, w.Pitch.Slug, w.Pitch.Name, w.Date, w.Hour, w.Status.ToString(),
        db.Set<WaitlistEntry>().Count(x => x.PitchId == w.PitchId && x.Date == w.Date && x.Hour == w.Hour && x.Status == WaitlistStatus.Waiting), w.CreatedAt));
}
