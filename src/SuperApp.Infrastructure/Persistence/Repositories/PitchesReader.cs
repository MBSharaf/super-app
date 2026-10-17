using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Read-only queries for the public pitch list and pitch page (live pitches only).</summary>
public sealed class PitchesReader(AppDbContext db) : IPitchesReader
{
    public async Task<PagedResult<PitchCardDto>> SearchAsync(PitchSearchQuery query, CancellationToken ct)
    {
        var pitches = Filter(Live(), query);
        var total = await pitches.CountAsync(ct);
        var page = await Sort(pitches, query).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(CardRow).AsNoTracking().ToListAsync(ct);
        var cards = page.Select(r => ToCard(r, query.Lat, query.Lng)).ToList();
        return new PagedResult<PitchCardDto>(cards, query.Page, query.PageSize, total);
    }

    public async Task<PitchDetailsDto?> DetailsAsync(string slug, CancellationToken ct)
    {
        var pitch = await Live().Where(p => p.Slug == slug)
            .Include(p => p.Photos).ThenInclude(ph => ph.File)
            .Include(p => p.OpeningHours).Include(p => p.PackageOptions).Include(p => p.CancellationPolicy)
            .Include(p => p.Venue).ThenInclude(v => v.Partner)
            .AsNoTracking().AsSplitQuery().FirstOrDefaultAsync(ct);
        if (pitch is null)
        {
            return null;
        }
        var card = ToCard(await Live().Where(p => p.Id == pitch.Id).Select(CardRow).SingleAsync(ct), null, null);
        var c = pitch.CancellationPolicy;
        return new PitchDetailsDto(
            card, pitch.Venue.Address, pitch.DescriptionEn, pitch.DescriptionAr, pitch.PeakFromHour, pitch.AllowCash,
            pitch.Photos.OrderBy(ph => ph.SortOrder).Select(ph => Photo(ph.File.Container, ph.File.StorageKey, ph.FileId)).ToList(),
            new PolicyDto(c.Key, c.NameEn, c.NameAr, c.FullRefundHours, c.HalfRefundHours, c.SummaryEn, c.SummaryAr),
            pitch.OpeningHours.OrderBy(h => h.DayOfWeek).Select(h => new OpeningHourDto(h.DayOfWeek, h.OpenHour, h.CloseHour, h.IsClosed)).ToList(),
            pitch.PackageOptions.Where(o => o.Status == PackageOptionStatus.Approved).OrderBy(o => o.MatchCount)
                .Select(o => new PackageOptionDto(o.Id, o.MatchCount, o.ApprovedDiscount ?? o.DiscountPercent)).ToList(),
            pitch.Venue.Partner.BusinessName);
    }

    private IQueryable<Pitch> Live() =>
        db.Set<Pitch>().Where(p => p.Status == PitchStatus.Live && p.Venue.Partner.Status == PartnerStatus.Active);

    private static IQueryable<Pitch> Filter(IQueryable<Pitch> q, PitchSearchQuery f)
    {
        if (!string.IsNullOrEmpty(f.Q))
        {
            q = q.Where(p => p.Name.Contains(f.Q) || p.Venue.Name.Contains(f.Q) || p.Venue.Area.NameEn.Contains(f.Q) || p.Venue.Area.NameAr.Contains(f.Q));
        }
        if (f.AreaId is not null) { q = q.Where(p => p.Venue.AreaId == f.AreaId); }
        if (f.GovernorateId is not null) { q = q.Where(p => p.Venue.Area.GovernorateId == f.GovernorateId); }
        if (f.Format is not null) { q = q.Where(p => p.Format == f.Format); }
        if (f.MinPrice is not null) { q = q.Where(p => p.PricePerHour >= f.MinPrice); }
        if (f.MaxPrice is not null) { q = q.Where(p => p.PricePerHour <= f.MaxPrice); }
        foreach (var key in (f.Facilities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct())
        {
            q = q.Where(p => p.Facilities.Any(pf => pf.Facility.Key == key));
        }
        return q;
    }

    private static IQueryable<Pitch> Sort(IQueryable<Pitch> q, PitchSearchQuery f) => f.Sort switch
    {
        "price" => q.OrderBy(p => p.PricePerHour).ThenBy(p => p.Id),
        "-price" => q.OrderByDescending(p => p.PricePerHour).ThenBy(p => p.Id),
        "name" => q.OrderBy(p => p.Name).ThenBy(p => p.Id),
        // Squared degrees are enough to order by distance; the card shows the real kilometres
        "distance" => q.OrderBy(p => ((p.Venue.Lat - f.Lat!.Value) * (p.Venue.Lat - f.Lat.Value)) + ((p.Venue.Lng - f.Lng!.Value) * (p.Venue.Lng - f.Lng.Value))).ThenBy(p => p.Id),
        _ => q.OrderByDescending(p => p.Rating).ThenByDescending(p => p.ReviewsCount).ThenBy(p => p.Id),
    };

    /// <summary>The columns a card needs, read in one query.</summary>
    private sealed record CardData(string Slug, string Name, string VenueName, string AreaEn, string AreaAr, string GovEn, decimal Lat, decimal Lng,
        byte Format, PitchSurface Surface, bool IsIndoor, decimal Price, decimal? Peak, decimal Rating, int Reviews,
        string? PhotoContainer, string? PhotoKey, long? PhotoId, List<string> Facilities);

    private static readonly System.Linq.Expressions.Expression<Func<Pitch, CardData>> CardRow = p => new CardData(
        p.Slug, p.Name, p.Venue.Name, p.Venue.Area.NameEn, p.Venue.Area.NameAr, p.Venue.Area.Governorate.NameEn, p.Venue.Lat, p.Venue.Lng,
        p.Format, p.Surface, p.IsIndoor, p.PricePerHour, p.PeakPricePerHour, p.Rating, p.ReviewsCount,
        p.Photos.OrderBy(ph => ph.SortOrder).Select(ph => ph.File.Container).FirstOrDefault(),
        p.Photos.OrderBy(ph => ph.SortOrder).Select(ph => ph.File.StorageKey).FirstOrDefault(),
        p.Photos.OrderBy(ph => ph.SortOrder).Select(ph => (long?)ph.FileId).FirstOrDefault(),
        p.Facilities.OrderBy(pf => pf.Facility.SortOrder).Select(pf => pf.Facility.Key).ToList());

    private static PitchCardDto ToCard(CardData r, decimal? lat, decimal? lng) => new(
        r.Slug, r.Name, r.VenueName, r.AreaEn, r.AreaAr, r.GovEn, r.Lat, r.Lng, r.Format, r.Surface.ToString(), r.IsIndoor, r.Price, r.Peak,
        r.Rating, r.Reviews, r.PhotoId is null ? null : Photo(r.PhotoContainer!, r.PhotoKey!, r.PhotoId.Value), r.Facilities,
        lat is null || lng is null ? null : Math.Round(Haversine((double)lat, (double)lng, (double)r.Lat, (double)r.Lng), 1));

    /// <summary>Demo photos are external ids (the site builds their address); uploads are served by /files.</summary>
    private static PhotoDto Photo(string container, string key, long fileId) =>
        container.StartsWith("demo", StringComparison.Ordinal) ? new PhotoDto(null, key) : new PhotoDto($"/api/v1/files/{fileId}", null);

    private static double Haversine(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371;
        double Rad(double d) => d * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) + (Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lng2 - lng1) / 2), 2));
        return 2 * R * Math.Asin(Math.Sqrt(a));
    }
}
