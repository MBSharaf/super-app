using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Partners;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Reviews;

/// <summary>A review in the partner dashboard. <c>Reported</c>: an open report is waiting for Super App.</summary>
public sealed record PartnerReviewDto(long Id, long PitchId, string PitchName, int Stars, string? Text, string CustomerName, string? BookingCode,
    string? Reply, DateTime? RepliedAt, bool IsHidden, bool Reported, DateTime CreatedAt);

public sealed record PartnerReviewsQuery(long? PitchId = null, int Page = 1, int PageSize = 20);

public sealed record ReplyRequest(string Text);

public sealed record ReportRequest(string Reason);

/// <summary>An open report for the moderators.</summary>
public sealed record ReviewReportDto(long Id, long ReviewId, string PitchName, int Stars, string? Text, string CustomerName, string Reason, DateTime CreatedAt);

/// <summary>
/// Pitch owners answer reviews in public and report the unfair ones (API §3.10); moderators (<c>reviews.moderate</c>)
/// hide a reported review or keep it. Hidden reviews leave the rating.
/// </summary>
public interface IPartnerReviewsService
{
    Task<PagedResult<PartnerReviewDto>> ListAsync(PartnerReviewsQuery query, CancellationToken ct);

    Task ReplyAsync(long id, ReplyRequest request, CancellationToken ct);

    Task RemoveReplyAsync(long id, CancellationToken ct);

    Task ReportAsync(long id, ReportRequest request, CancellationToken ct);

    Task<PagedResult<ReviewReportDto>> OpenReportsAsync(int page, int pageSize, CancellationToken ct);

    Task DecideAsync(long reportId, bool hide, CancellationToken ct);
}

public sealed class PartnerReviewsService(
    IPartnerAccess access,
    IReviewRepository reviews,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<ReplyRequest> replyRules,
    IValidator<ReportRequest> reportRules) : IPartnerReviewsService
{
    public async Task<PagedResult<PartnerReviewDto>> ListAsync(PartnerReviewsQuery query, CancellationToken ct) =>
        await reviews.ForPartnerAsync((await access.ViewAsync(ct)).PartnerId, query.PitchId, new PageRequest(query.Page, query.PageSize), ct);

    public async Task ReplyAsync(long id, ReplyRequest request, CancellationToken ct)
    {
        await replyRules.ValidateAndThrowAsync(request, ct);
        var review = await OwnAsync(id, ct);
        review.Reply = request.Text.Trim();
        review.RepliedAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(review.UserId, new NotificationMessage(NotificationCategory.Bookings, "The venue replied to your review",
            review.Reply.Length > 80 ? review.Reply[..80] + "…" : review.Reply, null), ct);
        await audit.LogAsync(AuditSystem.Partner, "review.replied", $"Reply to review {id}", "Review", id.ToString(), ct: ct);
    }

    public async Task RemoveReplyAsync(long id, CancellationToken ct)
    {
        var review = await OwnAsync(id, ct);
        review.Reply = null;
        review.RepliedAt = null;
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Partner, "review.reply_removed", $"Reply to review {id} removed", "Review", id.ToString(), ct: ct);
    }

    public async Task ReportAsync(long id, ReportRequest request, CancellationToken ct)
    {
        await reportRules.ValidateAndThrowAsync(request, ct);
        var review = await OwnAsync(id, ct);
        if (await reviews.OpenReportExistsAsync(id, ct))
        {
            throw new ConflictException("This review is already reported. Super App will decide soon.", "already_reported");
        }
        reviews.AddReport(new ReviewReport { ReviewId = review.Id, ReporterId = currentUser.UserId!.Value, Reason = request.Reason.Trim(), Status = ReviewReportStatus.Open });
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyStaffAsync("reviews.moderate", new NotificationMessage(NotificationCategory.System, "A review was reported",
            request.Reason.Trim(), "#/admin/reviews"), ct);
        await audit.LogAsync(AuditSystem.Partner, "review.reported", $"Review {id} reported", "Review", id.ToString(), ct: ct);
    }

    public Task<PagedResult<ReviewReportDto>> OpenReportsAsync(int page, int pageSize, CancellationToken ct) =>
        reviews.OpenReportsAsync(new PageRequest(page, pageSize), ct);

    public async Task DecideAsync(long reportId, bool hide, CancellationToken ct)
    {
        var report = await reviews.ReportAsync(reportId, ct) ?? throw new NotFoundException("Report not found.");
        if (report.Status != ReviewReportStatus.Open)
        {
            throw new BusinessRuleException("This report was already decided.", "wrong_status");
        }
        report.Status = hide ? ReviewReportStatus.Hidden : ReviewReportStatus.Kept;
        report.Review.IsHidden = hide;
        await unitOfWork.SaveChangesAsync(ct);
        await reviews.RecountAsync(report.Review.PitchId, ct);
        await notifier.NotifyUserAsync(report.ReporterId, new NotificationMessage(NotificationCategory.Bookings,
            hide ? "The review you reported was hidden" : "The review you reported stays visible",
            hide ? "It no longer counts in your rating." : "It follows the review rules.", "#/dash/reviews"), ct);
        await audit.LogAsync(AuditSystem.Admin, hide ? "review.hidden" : "review.kept", $"Report {reportId}: review {(hide ? "hidden" : "kept")}", "Review", report.ReviewId.ToString(), ct: ct);
    }

    private async Task<Review> OwnAsync(long id, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        return await reviews.PartnerReviewAsync(scope.PartnerId, id, ct) ?? throw new NotFoundException("Review not found.");
    }
}

public sealed class ReplyRequestValidator : AbstractValidator<ReplyRequest>
{
    public ReplyRequestValidator() => RuleFor(x => x.Text).NotEmpty().Length(2, 500);
}

public sealed class ReportRequestValidator : AbstractValidator<ReportRequest>
{
    public ReportRequestValidator() => RuleFor(x => x.Reason).NotEmpty().WithMessage("Tell Super App what’s wrong with this review.").Length(5, 300);
}
