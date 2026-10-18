using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Reviews;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Reviews, their replies and reports. The database allows one review per booking.</summary>
public sealed class ReviewRepository(AppDbContext db) : IReviewRepository
{
    public async Task<ReviewableBooking?> BookingAsync(string code, CancellationToken ct)
    {
        var booking = await db.Set<Booking>().FirstOrDefaultAsync(b => b.Code == code, ct);
        if (booking is null)
        {
            return null;
        }
        var info = await db.Set<Pitch>().IgnoreQueryFilters().Where(p => p.Id == booking.PitchId).Select(p => new { p.Name, p.Venue.Partner.UserId }).SingleAsync(ct);
        return new ReviewableBooking(booking, info.Name, info.UserId);
    }

    public Task<bool> ExistsForBookingAsync(long bookingId, CancellationToken ct) => db.Set<Review>().AnyAsync(r => r.BookingId == bookingId, ct);

    public Task<ReviewDto?> ForBookingAsync(long bookingId, CancellationToken ct) =>
        db.Set<Review>().Where(r => r.BookingId == bookingId).Select(Public).FirstOrDefaultAsync(ct);

    public void Add(Review review) => db.Set<Review>().Add(review);

    public async Task RecountAsync(long pitchId, CancellationToken ct)
    {
        var visible = db.Set<Review>().Where(r => r.PitchId == pitchId && !r.IsHidden);
        var count = await visible.CountAsync(ct);
        var average = count == 0 ? 0m : Math.Round((decimal)await visible.AverageAsync(r => (double)r.Stars, ct), 2);
        await db.Set<Pitch>().IgnoreQueryFilters().Where(p => p.Id == pitchId)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Rating, average).SetProperty(p => p.ReviewsCount, count), ct);
    }

    public Task<long?> LivePitchIdAsync(string slug, CancellationToken ct) =>
        db.Set<Pitch>().Where(p => p.Slug == slug && p.Status == PitchStatus.Live).Select(p => (long?)p.Id).FirstOrDefaultAsync(ct);

    public async Task<PagedResult<ReviewDto>> ForPitchAsync(long pitchId, PageRequest page, CancellationToken ct)
    {
        var query = db.Set<Review>().Where(r => r.PitchId == pitchId && !r.IsHidden);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedAt).Skip(page.Skip).Take(page.SafePageSize).Select(Public).ToListAsync(ct);
        return new PagedResult<ReviewDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public async Task<PagedResult<PartnerReviewDto>> ForPartnerAsync(long partnerId, long? pitchId, PageRequest page, CancellationToken ct)
    {
        var query = db.Set<Review>().Where(r => r.Pitch.Venue.PartnerId == partnerId && (pitchId == null || r.PitchId == pitchId));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.CreatedAt).Skip(page.Skip).Take(page.SafePageSize)
            .Select(r => new PartnerReviewDto(r.Id, r.PitchId, r.Pitch.Name, r.Stars, r.Text, r.User.FullName, r.Booking.Code, r.Reply, r.RepliedAt, r.IsHidden,
                r.Reports.Any(x => x.Status == ReviewReportStatus.Open), r.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<PartnerReviewDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public Task<Review?> PartnerReviewAsync(long partnerId, long reviewId, CancellationToken ct) =>
        db.Set<Review>().FirstOrDefaultAsync(r => r.Id == reviewId && r.Pitch.Venue.PartnerId == partnerId, ct);

    public void AddReport(ReviewReport report) => db.Set<ReviewReport>().Add(report);

    public Task<bool> OpenReportExistsAsync(long reviewId, CancellationToken ct) =>
        db.Set<ReviewReport>().AnyAsync(r => r.ReviewId == reviewId && r.Status == ReviewReportStatus.Open, ct);

    public async Task<PagedResult<ReviewReportDto>> OpenReportsAsync(PageRequest page, CancellationToken ct)
    {
        var query = db.Set<ReviewReport>().Where(r => r.Status == ReviewReportStatus.Open);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(r => r.CreatedAt).Skip(page.Skip).Take(page.SafePageSize)
            .Select(r => new ReviewReportDto(r.Id, r.ReviewId, r.Review.Pitch.Name, r.Review.Stars, r.Review.Text, r.Review.User.FullName, r.Reason, r.CreatedAt))
            .ToListAsync(ct);
        return new PagedResult<ReviewReportDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public Task<ReviewReport?> ReportAsync(long reportId, CancellationToken ct) =>
        db.Set<ReviewReport>().Include(r => r.Review).FirstOrDefaultAsync(r => r.Id == reportId, ct);

    /// <summary>Players see the first name only.</summary>
    private static readonly System.Linq.Expressions.Expression<Func<Review, ReviewDto>> Public = r => new ReviewDto(
        r.Id, r.Stars, r.Text, r.User.FullName.Contains(' ') ? r.User.FullName.Substring(0, r.User.FullName.IndexOf(' ')) : r.User.FullName,
        r.Reply, r.RepliedAt, r.CreatedAt);
}
