using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Demo 13: the pitch partners and their 12 pitches (one venue each) with facilities, photos and opening hours.
/// Hours after midnight (the site shows some pitches open until 2 AM) are cut at midnight for now — the
/// bookings phase decides how overnight hours are stored.
/// </summary>
public sealed class DemoPitchesSeeder(AppDbContext db) : ISeeder
{
    private sealed record PartnerRow(string User, string Business, string PayoutMethod, string PayoutDetail);
    private sealed record PitchRow(string Key, string Partner, string Name, string Area, string Address, decimal Lat, decimal Lng, byte Format,
        string Surface, bool Indoor, decimal Price, decimal? PeakPrice, string About, string Policy, decimal Rating, int Reviews,
        byte Open, byte Close, List<string> Facilities, List<string> Photos);
    private sealed record PitchesFile(List<PartnerRow> Partners, List<PitchRow> Pitches);

    public int Order => 13;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var file = SeedFile.Read<PitchesFile>("Demo/pitches.json");
        var users = await DemoRefs.UsersAsync(db, ct);
        var partners = file.Partners.Select(p => new Partner
        {
            UserId = users[p.User], BusinessName = p.Business, PayoutMethod = DemoRefs.Parse<PayoutMethod>(p.PayoutMethod), PayoutDetail = p.PayoutDetail,
        });
        var added = await db.Set<Partner>().AddMissingAsync(partners, p => p.UserId, ct);
        await db.SaveChangesAsync(ct);

        var existing = await db.Set<Pitch>().IgnoreQueryFilters().Select(p => p.Slug).ToListAsync(ct);
        foreach (var row in file.Pitches.Where(p => !existing.Contains(p.Key)))
        {
            await AddPitchAsync(row, users, ct);
            added++;
        }
        return added;
    }

    private async Task AddPitchAsync(PitchRow row, Dictionary<string, Guid> users, CancellationToken ct)
    {
        var partnerId = await db.Set<Partner>().Where(p => p.UserId == users[row.Partner]).Select(p => p.Id).SingleAsync(ct);
        var venue = new Venue
        {
            PartnerId = partnerId, Name = row.Name, Address = row.Address, Lat = row.Lat, Lng = row.Lng,
            AreaId = await db.Set<Area>().Where(a => a.NameEn == row.Area).Select(a => a.Id).SingleAsync(ct),
        };
        var pitch = new Pitch
        {
            Venue = venue, Slug = row.Key, Name = row.Name, Format = row.Format, Surface = DemoRefs.Parse<PitchSurface>(row.Surface), IsIndoor = row.Indoor,
            PricePerHour = row.Price, PeakPricePerHour = row.PeakPrice, PeakFromHour = row.PeakPrice is null ? null : (byte)18, DescriptionEn = row.About,
            CancellationPolicyId = await db.Set<CancellationPolicy>().Where(c => c.Key == row.Policy).Select(c => c.Id).SingleAsync(ct),
            Status = PitchStatus.Live, Rating = row.Rating, ReviewsCount = row.Reviews,
        };
        db.Set<Pitch>().Add(pitch);
        await db.SaveChangesAsync(ct);
        await AddPitchPartsAsync(pitch.Id, row, ct);
    }

    private async Task AddPitchPartsAsync(long pitchId, PitchRow row, CancellationToken ct)
    {
        var facilities = await db.Set<Facility>().Where(f => row.Facilities.Contains(f.Key)).Select(f => f.Id).ToListAsync(ct);
        db.Set<PitchFacility>().AddRange(facilities.Select(id => new PitchFacility { PitchId = pitchId, FacilityId = id }));
        var files = await DemoRefs.FilesAsync(db, ct);
        db.Set<PitchPhoto>().AddRange(row.Photos.Distinct().Select((key, i) => new PitchPhoto { PitchId = pitchId, FileId = files[key], SortOrder = i + 1 }));
        db.Set<OpeningHour>().AddRange(Enumerable.Range(0, 7).Select(day => new OpeningHour
        {
            PitchId = pitchId, DayOfWeek = (byte)day, OpenHour = row.Open, CloseHour = row.Close,
        }));
        await db.SaveChangesAsync(ct);
    }
}
