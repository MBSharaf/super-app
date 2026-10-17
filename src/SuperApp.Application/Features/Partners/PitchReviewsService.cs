using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>GET /admin/pitches — <c>Status</c>: WaitingForReview (default), Live, Rejected, ClosedByOwner, ClosedByAdmin.</summary>
public sealed record PitchReviewsQuery(string? Status = null, int Page = 1, int PageSize = 20);

public sealed record PitchReviewRowDto(long Id, string Slug, string Name, string BusinessName, string VenueName, string AreaEn, int Format,
    decimal PricePerHour, string Status, int PhotosCount, DateTime CreatedAt);

/// <summary>A pitch as the reviewer sees it: the partner, the venue and the whole form.</summary>
public sealed record PitchReviewDto(PitchReviewRowDto Summary, string Address, decimal Lat, decimal Lng, PitchEditDto Pitch);

public sealed record PitchRejectRequest(string Reason);

/// <summary>Who owns a pitch (for the review screen and the notifications).</summary>
public sealed record PitchOwner(long PartnerId, Guid OwnerUserId, string BusinessName);

public interface IPitchReviewRepository
{
    Task<PagedResult<PitchReviewRowDto>> ListAsync(PitchStatus status, PageRequest page, CancellationToken ct);

    Task<(PitchReviewRowDto Row, string Address, decimal Lat, decimal Lng, PitchOwner Owner)?> SummaryAsync(long pitchId, CancellationToken ct);

    /// <summary>The pitch, tracked for changes.</summary>
    Task<Pitch?> FindAsync(long pitchId, CancellationToken ct);
}

/// <summary>The admin's review of new pitches (permission <c>approvals.pitches</c>): approve → live, reject with a reason.</summary>
public interface IPitchReviewsService
{
    Task<PagedResult<PitchReviewRowDto>> ListAsync(PitchReviewsQuery query, CancellationToken ct);

    Task<PitchReviewDto> GetAsync(long id, CancellationToken ct);

    Task<PitchReviewDto> ApproveAsync(long id, CancellationToken ct);

    Task<PitchReviewDto> RejectAsync(long id, PitchRejectRequest request, CancellationToken ct);
}

public sealed class PitchReviewsService(
    IPitchReviewRepository reviews,
    IPartnerRepository partners,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IValidator<PitchRejectRequest> rejectRules) : IPitchReviewsService
{
    public Task<PagedResult<PitchReviewRowDto>> ListAsync(PitchReviewsQuery query, CancellationToken ct)
    {
        var status = Enum.TryParse<PitchStatus>(query.Status, out var s) && Enum.IsDefined(s) ? s : PitchStatus.WaitingForReview;
        return reviews.ListAsync(status, new PageRequest(query.Page, query.PageSize), ct);
    }

    public async Task<PitchReviewDto> GetAsync(long id, CancellationToken ct)
    {
        var summary = await reviews.SummaryAsync(id, ct) ?? throw new NotFoundException("Pitch not found.");
        var form = await partners.PitchForEditAsync(summary.Owner.PartnerId, id, ct) ?? throw new NotFoundException("Pitch not found.");
        return new PitchReviewDto(summary.Row, summary.Address, summary.Lat, summary.Lng, form);
    }

    public async Task<PitchReviewDto> ApproveAsync(long id, CancellationToken ct)
    {
        var (pitch, owner) = await WaitingAsync(id, ct);
        pitch.Status = PitchStatus.Live;
        pitch.RejectReason = null;
        pitch.ReviewedById = currentUser.UserId;
        await unitOfWork.SaveChangesAsync(ct);
        var result = await GetAsync(id, ct);
        await notifier.NotifyUserAsync(owner.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings, $"{pitch.Name} is live on Super App",
            "Players can find and book it now.", "#/dash/pitches", NotificationTone.Success), ct);
        await audit.LogAsync(AuditSystem.Admin, "pitch.approved", $"{pitch.Name} approved", "Pitch", pitch.Slug, ct: ct);
        return result;
    }

    public async Task<PitchReviewDto> RejectAsync(long id, PitchRejectRequest request, CancellationToken ct)
    {
        await rejectRules.ValidateAndThrowAsync(request, ct);
        var (pitch, owner) = await WaitingAsync(id, ct);
        pitch.Status = PitchStatus.Rejected;
        pitch.RejectReason = request.Reason.Trim();
        pitch.ReviewedById = currentUser.UserId;
        await unitOfWork.SaveChangesAsync(ct);
        var result = await GetAsync(id, ct);
        await notifier.NotifyUserAsync(owner.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings, $"{pitch.Name} needs changes",
            $"{pitch.RejectReason} — edit the pitch and it goes back to review.", "#/dash/pitches", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, "pitch.rejected", $"{pitch.Name} rejected: {pitch.RejectReason}", "Pitch", pitch.Slug, ct: ct);
        return result;
    }

    private async Task<(Pitch Pitch, PitchOwner Owner)> WaitingAsync(long id, CancellationToken ct)
    {
        var summary = await reviews.SummaryAsync(id, ct) ?? throw new NotFoundException("Pitch not found.");
        var pitch = await reviews.FindAsync(id, ct) ?? throw new NotFoundException("Pitch not found.");
        return pitch.Status == PitchStatus.WaitingForReview ? (pitch, summary.Owner)
            : throw new BusinessRuleException($"This pitch is {pitch.Status}; only pitches waiting for review can be approved or rejected.", "wrong_status");
    }
}

public sealed class PitchRejectRequestValidator : AbstractValidator<PitchRejectRequest>
{
    public PitchRejectRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Tell the partner what to change.").Length(5, 400);
    }
}
