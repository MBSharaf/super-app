using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Reviews;

/// <summary>A review as players see it on the pitch page. <c>CustomerName</c> is the first name only.</summary>
public sealed record ReviewDto(long Id, int Stars, string? Text, string CustomerName, string? Reply, DateTime? RepliedAt, DateTime CreatedAt);

/// <summary>POST /bookings/{code}/review — 1–5 stars and a few words (10–500 characters).</summary>
public sealed record ReviewRequest(int Stars, string Text);

public sealed record ReviewsQuery(int Page = 1, int PageSize = 10);

/// <summary>What reviewing needs about a booking.</summary>
public sealed record ReviewableBooking(Booking Booking, string PitchName, Guid OwnerUserId);

public interface IReviewRepository
{
    Task<ReviewableBooking?> BookingAsync(string code, CancellationToken ct);

    Task<bool> ExistsForBookingAsync(long bookingId, CancellationToken ct);

    Task<ReviewDto?> ForBookingAsync(long bookingId, CancellationToken ct);

    void Add(Review review);

    /// <summary>Recounts the pitch's rating and number of reviews from its visible reviews.</summary>
    Task RecountAsync(long pitchId, CancellationToken ct);

    Task<long?> LivePitchIdAsync(string slug, CancellationToken ct);

    Task<PagedResult<ReviewDto>> ForPitchAsync(long pitchId, PageRequest page, CancellationToken ct);

    Task<PagedResult<PartnerReviewDto>> ForPartnerAsync(long partnerId, long? pitchId, PageRequest page, CancellationToken ct);

    /// <summary>A review of one of the partner's pitches, tracked for changes.</summary>
    Task<Review?> PartnerReviewAsync(long partnerId, long reviewId, CancellationToken ct);

    void AddReport(ReviewReport report);

    Task<bool> OpenReportExistsAsync(long reviewId, CancellationToken ct);

    Task<PagedResult<ReviewReportDto>> OpenReportsAsync(PageRequest page, CancellationToken ct);

    /// <summary>A report with its review, tracked for changes.</summary>
    Task<ReviewReport?> ReportAsync(long reportId, CancellationToken ct);
}

/// <summary>Players review a game they played (API §3.10) and read a pitch's reviews.</summary>
public interface IReviewsService
{
    Task<ReviewDto> CreateAsync(string bookingCode, ReviewRequest request, CancellationToken ct);

    Task<ReviewDto?> MineAsync(string bookingCode, CancellationToken ct);

    Task<PagedResult<ReviewDto>> ForPitchAsync(string slug, ReviewsQuery query, CancellationToken ct);
}

public sealed class ReviewsService(
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<ReviewRequest> rules,
    Money.LoyaltyService loyalty) : IReviewsService
{
    public async Task<ReviewDto> CreateAsync(string bookingCode, ReviewRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var target = await OwnAsync(bookingCode, ct);
        var b = target.Booking;
        var end = b.Date.ToDateTime(new TimeOnly(b.StartHour, 0)).AddHours(b.Duration);
        if (b.Status is BookingStatus.CancelledByCustomer or BookingStatus.CancelledByVenue or BookingStatus.NoShow || end > CairoTime.Now(clock))
        {
            throw new BusinessRuleException("You can review a pitch after you’ve played there.", "not_played");
        }
        if (await reviews.ExistsForBookingAsync(b.Id, ct))
        {
            throw new ConflictException("You already reviewed this game.", "already_reviewed");
        }
        reviews.Add(new Review { PitchId = b.PitchId, BookingId = b.Id, UserId = b.CustomerId!.Value, Stars = (byte)request.Stars, Text = request.Text.Trim() });
        await loyalty.EarnForReviewAsync(b.CustomerId!.Value, b.Id, b.Code, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await reviews.RecountAsync(b.PitchId, ct);
        await notifier.NotifyUserAsync(target.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings, $"New {request.Stars}★ review for {target.PitchName}",
            request.Text.Trim().Length > 80 ? request.Text.Trim()[..80] + "…" : request.Text.Trim(), "#/dash/reviews"), ct);
        await audit.LogAsync(AuditSystem.Site, "review.created", $"{request.Stars}★ review for {target.PitchName}", "Booking", b.Code, ct: ct);
        return (await reviews.ForBookingAsync(b.Id, ct))!;
    }

    public async Task<ReviewDto?> MineAsync(string bookingCode, CancellationToken ct) =>
        await reviews.ForBookingAsync((await OwnAsync(bookingCode, ct)).Booking.Id, ct);

    public async Task<PagedResult<ReviewDto>> ForPitchAsync(string slug, ReviewsQuery query, CancellationToken ct)
    {
        var pitchId = await reviews.LivePitchIdAsync(slug, ct) ?? throw new NotFoundException("This pitch does not exist.");
        return await reviews.ForPitchAsync(pitchId, new PageRequest(query.Page, query.PageSize), ct);
    }

    private async Task<ReviewableBooking> OwnAsync(string code, CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        var target = await reviews.BookingAsync(code, ct);
        return target is not null && target.Booking.CustomerId == me ? target : throw new NotFoundException("This booking does not exist.");
    }
}

public sealed class ReviewRequestValidator : AbstractValidator<ReviewRequest>
{
    public ReviewRequestValidator()
    {
        RuleFor(x => x.Stars).InclusiveBetween(1, 5).WithMessage("Choose 1 to 5 stars.");
        RuleFor(x => x.Text).NotEmpty().WithMessage("Tell other players a little more (at least 10 characters).")
            .Must(t => t.Trim().Length is >= 10 and <= 500).WithMessage("Tell other players a little more (10 to 500 characters).");
    }
}
