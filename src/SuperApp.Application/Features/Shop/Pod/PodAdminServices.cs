using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>Design review (API §7.2 "Designs", FR-POD-06): the queue (Pro first, then normal, then Elite spot checks), approve, reject with a reason.</summary>
public interface IDesignReviewService
{
    Task<PagedResult<ReviewDesignDto>> QueueAsync(int page, int pageSize, CancellationToken ct);

    Task ApproveAsync(long id, CancellationToken ct);

    Task RejectAsync(long id, RejectRequest request, CancellationToken ct);
}

public sealed class DesignReviewService(
    IPodAdminRepository admin,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<RejectRequest> rules) : IDesignReviewService
{
    public Task<PagedResult<ReviewDesignDto>> QueueAsync(int page, int pageSize, CancellationToken ct) =>
        admin.QueueAsync(Math.Max(1, page), pageSize is < 1 or > 100 ? 20 : pageSize, ct);

    /// <summary>A design in review goes live; an Elite spot check is cleared.</summary>
    public async Task ApproveAsync(long id, CancellationToken ct)
    {
        var design = await WaitingAsync(id, ct);
        var wasLive = design.Status == DesignStatus.Live;
        (design.Status, design.SpotCheck, design.ReviewedAt, design.ReviewedById, design.RejectReason) = (DesignStatus.Live, false, clock.UtcNow, currentUser.UserId, null);
        await unitOfWork.SaveChangesAsync(ct);
        if (!wasLive)
        {
            await notifier.NotifyUserAsync(design.Seller.UserId, new NotificationMessage(NotificationCategory.Designs, $"“{design.Title}” is live",
                "Your design passed review and is on sale now.", "#/seller/designs", NotificationTone.Success), ct);
        }
        await audit.LogAsync(AuditSystem.Admin, "design.approved", $"Approved “{design.Title}”{(wasLive ? " (spot check)" : "")}", "Design", Id(id), ct: ct);
    }

    public async Task RejectAsync(long id, RejectRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var design = await WaitingAsync(id, ct);
        (design.Status, design.SpotCheck, design.ReviewedAt, design.ReviewedById, design.RejectReason) = (DesignStatus.Rejected, false, clock.UtcNow, currentUser.UserId, request.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(design.Seller.UserId, new NotificationMessage(NotificationCategory.Designs, $"“{design.Title}” needs changes",
            design.RejectReason, "#/seller/designs", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, "design.rejected", $"Rejected “{design.Title}”: {design.RejectReason}", "Design", Id(id), ct: ct);
    }

    private async Task<Design> WaitingAsync(long id, CancellationToken ct)
    {
        var design = await admin.DesignAsync(id, ct) ?? throw new NotFoundException("Design not found.");
        return design.Status == DesignStatus.InReview || (design.Status == DesignStatus.Live && design.SpotCheck)
            ? design : throw new BusinessRuleException("This design is not waiting for review.", "wrong_status");
    }

    private static string Id(long id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>Design reports and strikes (FR-POD-07): take down (copyright strike freezes the level 90 days; 3 active strikes suspend) or dismiss.</summary>
public interface IDesignReportsService
{
    Task<PagedResult<DesignReportDto>> ListAsync(int page, int pageSize, CancellationToken ct);

    Task TakeDownAsync(long reportId, TakeDownRequest request, CancellationToken ct);

    Task DismissAsync(long reportId, CancellationToken ct);
}

public sealed class DesignReportsService(
    IPodAdminRepository admin,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<TakeDownRequest> rules) : IDesignReportsService
{
    public const int StrikesToSuspend = 3;

    public Task<PagedResult<DesignReportDto>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        admin.ReportsAsync(Math.Max(1, page), pageSize is < 1 or > 100 ? 20 : pageSize, ct);

    public async Task TakeDownAsync(long reportId, TakeDownRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var report = await OpenAsync(reportId, ct);
        var design = await admin.DesignAsync(report.DesignId, ct) ?? throw new NotFoundException();
        design.Status = DesignStatus.Removed;
        await CloseReportsAsync(design.Id, DesignReportStatus.TakenDown, ct);
        var suspended = request.Strike && await StrikeAsync(design, request.Reason.Trim(), ct);
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyUserAsync(design.Seller.UserId, new NotificationMessage(NotificationCategory.Designs, $"“{design.Title}” was taken down",
            request.Reason.Trim() + (request.Strike ? " · copyright strike: your level is frozen for 90 days." : "") + (suspended ? " Your store is suspended after 3 strikes." : ""),
            "#/seller/designs", NotificationTone.Danger), ct);
        await audit.LogAsync(AuditSystem.Admin, "design.taken_down", $"Took down “{design.Title}”{(request.Strike ? " with a strike" : "")}: {request.Reason.Trim()}", "Design",
            design.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    public async Task DismissAsync(long reportId, CancellationToken ct)
    {
        var report = await OpenAsync(reportId, ct);
        (report.Status, report.HandledById, report.HandledAt) = (DesignReportStatus.Dismissed, currentUser.UserId, clock.UtcNow);
        await unitOfWork.SaveChangesAsync(ct);
        await audit.LogAsync(AuditSystem.Admin, "design_report.dismissed", $"Kept design #{report.DesignId} after a report", "DesignReport",
            reportId.ToString(System.Globalization.CultureInfo.InvariantCulture), ct: ct);
    }

    private async Task<DesignReport> OpenAsync(long id, CancellationToken ct)
    {
        var report = await admin.ReportAsync(id, ct) ?? throw new NotFoundException("Report not found.");
        return report.Status == DesignReportStatus.Open ? report : throw new BusinessRuleException("This report is already handled.", "wrong_status");
    }

    private async Task CloseReportsAsync(long designId, DesignReportStatus status, CancellationToken ct)
    {
        foreach (var r in await admin.OpenReportsAsync(designId, ct))
        {
            (r.Status, r.HandledById, r.HandledAt) = (status, currentUser.UserId, clock.UtcNow);
        }
    }

    /// <summary>Adds the strike and freezes the level; returns true when the seller is now suspended.</summary>
    private async Task<bool> StrikeAsync(Design design, string reason, CancellationToken ct)
    {
        admin.AddStrike(new SellerStrike { SellerId = design.SellerId, DesignId = design.Id, Reason = reason, ExpiresAt = clock.UtcNow.AddYears(1) });
        var seller = design.Seller;
        seller.FrozenUntil = clock.UtcNow.AddDays(90);
        var strikes = await admin.ActiveStrikesAsync(seller.Id, clock.UtcNow, ct) + 1;
        if (strikes >= StrikesToSuspend)
        {
            seller.Status = SellerStatus.Suspended;
            seller.Store.Status = StoreStatus.Suspended;
            return true;
        }
        seller.Status = SellerStatus.Frozen;
        return false;
    }
}

public sealed class RejectRequestValidator : AbstractValidator<RejectRequest>
{
    public RejectRequestValidator() => RuleFor(x => x.Reason).NotEmpty().Length(5, 400);
}

public sealed class TakeDownRequestValidator : AbstractValidator<TakeDownRequest>
{
    public TakeDownRequestValidator() => RuleFor(x => x.Reason).NotEmpty().Length(5, 400);
}
