using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Packages;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Package sizes and packages. A package and all its matches are saved together, or not at all.</summary>
public sealed class PackageRepository(AppDbContext db, IClock clock) : IPackageRepository
{
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<IReadOnlyList<PartnerPackageOptionDto>> OptionsAsync(long pitchId, CancellationToken ct) =>
        await db.Set<PackageOption>().Where(o => o.PitchId == pitchId && o.Status != PackageOptionStatus.Disabled).OrderBy(o => o.MatchCount)
            .Select(o => new PartnerPackageOptionDto(o.Id, o.MatchCount, o.DiscountPercent, o.Status.ToString(), o.ApprovedDiscount, o.RejectReason))
            .ToListAsync(ct);

    public Task<List<PackageOption>> OptionRowsAsync(long pitchId, CancellationToken ct) =>
        db.Set<PackageOption>().Where(o => o.PitchId == pitchId).ToListAsync(ct);

    public void AddOption(PackageOption option) => db.Set<PackageOption>().Add(option);

    public async Task<PagedResult<PackageOptionReviewDto>> WaitingOptionsAsync(PageRequest page, CancellationToken ct)
    {
        var query = db.Set<PackageOption>().Where(o => o.Status == PackageOptionStatus.WaitingForApproval);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(o => o.UpdatedAt ?? o.CreatedAt).Skip(page.Skip).Take(page.SafePageSize)
            .Select(o => new PackageOptionReviewDto(o.Id, o.PitchId, o.Pitch.Name, o.Pitch.Venue.Partner.BusinessName, o.MatchCount, o.DiscountPercent,
                o.Pitch.PricePerHour, o.Status.ToString(), o.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<PackageOptionReviewDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<(PackageOption Option, string PitchName, Guid OwnerUserId)?> OptionForReviewAsync(long id, CancellationToken ct)
    {
        var option = await db.Set<PackageOption>().FirstOrDefaultAsync(o => o.Id == id, ct);
        if (option is null)
        {
            return null;
        }
        var info = await db.Set<Pitch>().Where(p => p.Id == option.PitchId).Select(p => new { p.Name, p.Venue.Partner.UserId }).SingleAsync(ct);
        return (option, info.Name, info.UserId);
    }

    public Task<PackageOption?> ApprovedOptionAsync(string pitchSlug, long optionId, CancellationToken ct) =>
        db.Set<PackageOption>().AsNoTracking().FirstOrDefaultAsync(o => o.Id == optionId && o.Pitch.Slug == pitchSlug
            && o.Status == PackageOptionStatus.Approved && o.Pitch.Status == PitchStatus.Live, ct);

    public async Task SaveAsync(Package package, IReadOnlyList<(Booking Booking, IReadOnlyList<(DateOnly Date, int Hour)> Slots)> matches, CancellationToken ct)
    {
        db.Set<Package>().Add(package);
        foreach (var (booking, slots) in matches)
        {
            db.Set<Booking>().Add(booking);
            db.Set<BookingSlot>().AddRange(slots.Select(s => new BookingSlot { Booking = booking, PitchId = booking.PitchId, Date = s.Date, Hour = (byte)s.Hour, IsActive = true }));
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("Someone just booked one of these weeks. Please check the dates again.", "slot_taken");
        }
    }

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "PK-" + new string(Enumerable.Range(0, 8).Select(_ => CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]).ToArray());
            if (!await db.Set<Package>().AnyAsync(p => p.Code == code, ct))
            {
                return code;
            }
        }
    }

    public Task<Package?> RecentSameAsync(Guid customerId, long pitchId, DateOnly startsOn, int startHour, CancellationToken ct)
    {
        var since = clock.UtcNow.AddMinutes(-10);
        return db.Set<Package>().FirstOrDefaultAsync(p => p.CustomerId == customerId && p.PitchId == pitchId && p.StartsOn == startsOn
            && p.StartHour == startHour && p.CreatedAt >= since, ct);
    }

    public async Task<IReadOnlyList<PackageDto>> MineAsync(Guid customerId, CancellationToken ct) =>
        (await Rows(db.Set<Package>().Where(p => p.CustomerId == customerId).OrderByDescending(p => p.CreatedAt)).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<PackageDto?> GetAsync(Guid customerId, string code, CancellationToken ct)
    {
        var row = await Rows(db.Set<Package>().Where(p => p.CustomerId == customerId && p.Code == code)).FirstOrDefaultAsync(ct);
        return row is null ? null : ToDto(row);
    }

    private sealed record Row(Package P, string Slug, string Name, List<Booking> Matches);

    private IQueryable<Row> Rows(IQueryable<Package> q) => q.AsNoTracking().Select(p => new Row(p, p.Pitch.Slug, p.Pitch.Name,
        db.Set<Booking>().Where(b => b.PackageId == p.Id).OrderBy(b => b.Date).ThenBy(b => b.StartHour).ToList()));

    private static PackageDto ToDto(Row r) => new(
        r.P.Code, r.Slug, r.Name, r.P.MatchCount, r.P.UsedCount, r.P.DayOfWeek, r.P.StartHour, r.P.Duration, r.P.StartsOn, r.P.EndsOn,
        r.P.DiscountPercent, r.P.TotalPrice, r.P.Status.ToString(),
        r.Matches.Select((b, i) => new PackageMatchDto(i + 1, b.Code, b.Date, b.StartHour, b.Status.ToString(), b.CheckedInAt)).ToList(), null);
}
