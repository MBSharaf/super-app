using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Partners;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>A partner's venues, pitches and closures. Every query is limited to the partner's own rows.</summary>
public sealed class PartnerRepository(AppDbContext db) : IPartnerRepository
{
    public async Task<PartnerScope?> ScopeAsync(Guid userId, CancellationToken ct) =>
        await db.Set<Partner>().Where(p => p.UserId == userId)
            .Select(p => new PartnerScope(p.Id, p.UserId, p.BusinessName, p.Status, (PartnerStaffRole?)null)).FirstOrDefaultAsync(ct)
        ?? await db.Set<PartnerStaffMember>().Where(s => s.UserId == userId && s.IsActive)
            .Select(s => new PartnerScope(s.PartnerId, s.Partner.UserId, s.Partner.BusinessName, s.Partner.Status, (PartnerStaffRole?)s.Role)).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<VenueDto>> VenuesAsync(long partnerId, CancellationToken ct) =>
        await db.Set<Venue>().Where(v => v.PartnerId == partnerId).OrderBy(v => v.Name)
            .Select(v => new VenueDto(v.Id, v.Name, v.AreaId, v.Area.NameEn, v.Area.NameAr, v.Address, v.Lat, v.Lng, v.Phone,
                db.Set<Pitch>().Count(p => p.VenueId == v.Id)))
            .ToListAsync(ct);

    public Task<Venue?> VenueAsync(long partnerId, long venueId, CancellationToken ct) =>
        db.Set<Venue>().FirstOrDefaultAsync(v => v.Id == venueId && v.PartnerId == partnerId, ct);

    public void AddVenue(Venue venue) => db.Set<Venue>().Add(venue);

    public Task<bool> AreaExistsAsync(int areaId, CancellationToken ct) => db.Set<Area>().AnyAsync(a => a.Id == areaId, ct);

    public async Task<IReadOnlyList<MyPitchDto>> PitchesAsync(long partnerId, CancellationToken ct)
    {
        var rows = await db.Set<Pitch>().Where(p => p.Venue.PartnerId == partnerId).OrderBy(p => p.Venue.Name).ThenBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Slug, p.Name, p.VenueId, Venue = p.Venue.Name, p.Format, p.PricePerHour, p.PeakPricePerHour, p.Status, p.RejectReason,
                p.Rating, p.ReviewsCount,
                Photo = p.Photos.OrderBy(ph => ph.SortOrder).Select(ph => new { ph.FileId, ph.File.Container, ph.File.StorageKey }).FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows.Select(r => new MyPitchDto(r.Id, r.Slug, r.Name, r.VenueId, r.Venue, r.Format, r.PricePerHour, r.PeakPricePerHour, r.Status.ToString(),
            r.RejectReason, r.Rating, r.ReviewsCount, r.Photo is null ? null : PitchesReader.Photo(r.Photo.Container, r.Photo.StorageKey, r.Photo.FileId))).ToList();
    }

    public async Task<PitchEditDto?> PitchForEditAsync(long partnerId, long pitchId, CancellationToken ct)
    {
        var p = await db.Set<Pitch>().AsNoTracking().AsSplitQuery().Where(x => x.Id == pitchId && x.Venue.PartnerId == partnerId)
            .Include(x => x.CancellationPolicy).Include(x => x.OpeningHours).Include(x => x.Facilities).ThenInclude(f => f.Facility)
            .Include(x => x.Photos).ThenInclude(ph => ph.File)
            .FirstOrDefaultAsync(ct);
        if (p is null)
        {
            return null;
        }
        var hours = p.OpeningHours.OrderBy(h => h.DayOfWeek).Select(h => new OpeningHourDto(h.DayOfWeek, h.OpenHour, h.CloseHour, h.IsClosed)).ToList();
        var photos = p.Photos.OrderBy(ph => ph.SortOrder).Select(ph => PitchesReader.Photo(ph.File.Container, ph.File.StorageKey, ph.FileId)).ToList();
        return new PitchEditDto(p.Id, p.Slug, p.VenueId, p.Name, p.Format, p.Surface.ToString(), p.IsIndoor, p.PricePerHour, p.PeakPricePerHour, p.PeakFromHour,
            p.DescriptionEn, p.DescriptionAr, p.CancellationPolicy.Key, p.AllowCash, p.Facilities.OrderBy(f => f.Facility.SortOrder).Select(f => f.Facility.Key).ToList(),
            hours, photos, p.Status.ToString(), p.RejectReason, Convert.ToBase64String(p.RowVersion));
    }

    public Task<Pitch?> PitchAsync(long partnerId, long pitchId, CancellationToken ct) =>
        db.Set<Pitch>().AsSplitQuery().Include(p => p.OpeningHours).Include(p => p.Facilities)
            .FirstOrDefaultAsync(p => p.Id == pitchId && p.Venue.PartnerId == partnerId, ct);

    public void ExpectVersion(Pitch pitch, byte[] rowVersion) => db.Entry(pitch).Property(p => p.RowVersion).OriginalValue = rowVersion;

    public void AddPitch(Pitch pitch) => db.Set<Pitch>().Add(pitch);

    public async Task<string> FreeSlugAsync(string name, CancellationToken ct)
    {
        var slug = OnboardingRepository.Slug(name, "pitch");
        var taken = await db.Set<Pitch>().IgnoreQueryFilters().Where(p => p.Slug.StartsWith(slug)).Select(p => p.Slug).ToListAsync(ct);
        return OnboardingRepository.Free(slug, taken);
    }

    public async Task<int?> PolicyIdAsync(string key, CancellationToken ct) =>
        await db.Set<CancellationPolicy>().Where(p => p.Key == key).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<string, int>> FacilityIdsAsync(IReadOnlyList<string> keys, CancellationToken ct) =>
        await db.Set<Facility>().Where(f => keys.Contains(f.Key)).ToDictionaryAsync(f => f.Key, f => f.Id, ct);

    public async Task<IReadOnlyList<ClosureDto>> ClosuresAsync(long pitchId, DateOnly from, CancellationToken ct) =>
        await db.Set<Closure>().Where(c => c.PitchId == pitchId && c.ToDate >= from).OrderBy(c => c.FromDate).ThenBy(c => c.FromHour)
            .Select(c => new ClosureDto(c.Id, c.FromDate, c.ToDate, c.FromHour, c.ToHour, c.Reason, c.CreatedAt))
            .ToListAsync(ct);

    public Task<Closure?> ClosureAsync(long pitchId, long closureId, CancellationToken ct) =>
        db.Set<Closure>().FirstOrDefaultAsync(c => c.Id == closureId && c.PitchId == pitchId, ct);

    public void AddClosure(Closure closure) => db.Set<Closure>().Add(closure);

    public void RemoveClosure(Closure closure) => db.Set<Closure>().Remove(closure);

    public Task<int> BookingsInRangeAsync(long pitchId, DateOnly from, DateOnly to, int? fromHour, int? toHour, CancellationToken ct) =>
        db.Set<BookingSlot>()
            .Where(s => s.PitchId == pitchId && s.IsActive && s.Date >= from && s.Date <= to && (fromHour == null || (s.Hour >= fromHour && s.Hour < toHour)))
            .Select(s => s.BookingId).Distinct().CountAsync(ct);
}
