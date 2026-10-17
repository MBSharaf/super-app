using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Partners;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Packages;

/// <summary>
/// Package sizes (API §3.2): the partner offers 1–12 matches with a discount up to the CEO's maximum (setting
/// <c>package.maxDiscount</c>); each new or changed size waits for an admin (<c>approvals.packages</c>) before players see it.
/// </summary>
public interface IPackageOptionsService
{
    Task<IReadOnlyList<PartnerPackageOptionDto>> ListAsync(long pitchId, CancellationToken ct);

    Task<IReadOnlyList<PartnerPackageOptionDto>> SaveAsync(long pitchId, PackageOptionsRequest request, CancellationToken ct);

    Task<PagedResult<PackageOptionReviewDto>> WaitingAsync(int page, int pageSize, CancellationToken ct);

    Task ApproveAsync(long id, ApproveOptionRequest request, CancellationToken ct);

    Task RejectAsync(long id, RejectOptionRequest request, CancellationToken ct);
}

public sealed class PackageOptionsService(
    IPartnerAccess access,
    IPartnerRepository partners,
    IPackageRepository packages,
    ISettingsReader settings,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<PackageOptionsRequest> rules,
    IValidator<RejectOptionRequest> rejectRules) : IPackageOptionsService
{
    public async Task<IReadOnlyList<PartnerPackageOptionDto>> ListAsync(long pitchId, CancellationToken ct)
    {
        var scope = await access.ViewAsync(ct);
        _ = await partners.PitchAsync(scope.PartnerId, pitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        return await packages.OptionsAsync(pitchId, ct);
    }

    public async Task<IReadOnlyList<PartnerPackageOptionDto>> SaveAsync(long pitchId, PackageOptionsRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        var pitch = await partners.PitchAsync(scope.PartnerId, pitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        var max = await settings.DecimalAsync("package.maxDiscount", 25, ct);
        if (request.Options.FirstOrDefault(o => o.DiscountPercent > max) is { } over)
        {
            throw new BusinessRuleException($"The highest package discount is {max:0}% ({over.MatchCount} matches asks {over.DiscountPercent:0}%).", "discount_over_max");
        }
        var rows = await packages.OptionRowsAsync(pitchId, ct);
        var waiting = Merge(pitchId, rows, request.Options);
        await unitOfWork.SaveChangesAsync(ct);
        if (waiting > 0)
        {
            await notifier.NotifyStaffAsync("approvals.packages", new NotificationMessage(NotificationCategory.System,
                $"Package discounts to approve: {pitch.Name}", $"{scope.BusinessName} · {waiting} size(s)", "#/admin/approvals?tab=packages"), ct);
        }
        await audit.LogAsync(AuditSystem.Partner, "packages.updated", $"Package sizes of {pitch.Name} saved ({waiting} waiting for approval)", "Pitch", pitch.Slug, ct: ct);
        return await packages.OptionsAsync(pitchId, ct);
    }

    /// <summary>New or changed sizes wait for approval; sizes left out are switched off. Returns how many wait.</summary>
    private int Merge(long pitchId, List<PackageOption> rows, IReadOnlyList<PackageOptionInput> wanted)
    {
        var waiting = 0;
        foreach (var input in wanted)
        {
            var row = rows.FirstOrDefault(r => r.MatchCount == input.MatchCount);
            if (row is null)
            {
                row = new PackageOption { PitchId = pitchId, MatchCount = (byte)input.MatchCount };
                packages.AddOption(row);
            }
            else if (row.Status == PackageOptionStatus.Approved && row.DiscountPercent == input.DiscountPercent)
            {
                continue;
            }
            row.DiscountPercent = input.DiscountPercent;
            row.Status = PackageOptionStatus.WaitingForApproval;
            row.ApprovedDiscount = null;
            row.RejectReason = null;
            waiting++;
        }
        foreach (var gone in rows.Where(r => wanted.All(w => w.MatchCount != r.MatchCount)))
        {
            gone.Status = PackageOptionStatus.Disabled;
        }
        return waiting;
    }

    public Task<PagedResult<PackageOptionReviewDto>> WaitingAsync(int page, int pageSize, CancellationToken ct) =>
        packages.WaitingOptionsAsync(new PageRequest(page, pageSize), ct);

    public async Task ApproveAsync(long id, ApproveOptionRequest request, CancellationToken ct)
    {
        var (option, pitchName, owner) = await WaitingAsync(id, ct);
        var discount = request.ApprovedDiscount ?? option.DiscountPercent;
        if (discount < 0 || discount > option.DiscountPercent)
        {
            throw new BadRequestException("The approved discount must be between 0 and what the partner asked.", "approved_discount");
        }
        option.Status = PackageOptionStatus.Approved;
        option.ApprovedDiscount = discount;
        await ReviewedAsync(option, ct);
        await notifier.NotifyUserAsync(owner, new NotificationMessage(NotificationCategory.Bookings, $"Package of {option.MatchCount} matches approved at {pitchName}",
            $"Players can buy it now with {discount:0}% off.", "#/dash/pitches", NotificationTone.Success), ct);
        await audit.LogAsync(AuditSystem.Admin, "package_option.approved", $"{pitchName}: {option.MatchCount} matches at {discount:0}%", "PackageOption", id.ToString(), ct: ct);
    }

    public async Task RejectAsync(long id, RejectOptionRequest request, CancellationToken ct)
    {
        await rejectRules.ValidateAndThrowAsync(request, ct);
        var (option, pitchName, owner) = await WaitingAsync(id, ct);
        option.Status = PackageOptionStatus.Rejected;
        option.RejectReason = request.Reason.Trim();
        await ReviewedAsync(option, ct);
        await notifier.NotifyUserAsync(owner, new NotificationMessage(NotificationCategory.Bookings, $"Package of {option.MatchCount} matches not approved at {pitchName}",
            option.RejectReason, "#/dash/pitches", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Admin, "package_option.rejected", $"{pitchName}: {option.MatchCount} matches — {option.RejectReason}", "PackageOption", id.ToString(), ct: ct);
    }

    private async Task<(PackageOption Option, string PitchName, Guid Owner)> WaitingAsync(long id, CancellationToken ct)
    {
        var found = await packages.OptionForReviewAsync(id, ct) ?? throw new NotFoundException("Package size not found.");
        return found.Option.Status == PackageOptionStatus.WaitingForApproval ? (found.Option, found.PitchName, found.OwnerUserId)
            : throw new BusinessRuleException("Only sizes waiting for approval can be approved or rejected.", "wrong_status");
    }

    private Task<int> ReviewedAsync(PackageOption option, CancellationToken ct)
    {
        option.ReviewedById = currentUser.UserId;
        option.ReviewedAt = clock.UtcNow;
        return unitOfWork.SaveChangesAsync(ct);
    }
}

public sealed class PackageOptionsRequestValidator : AbstractValidator<PackageOptionsRequest>
{
    public PackageOptionsRequestValidator()
    {
        RuleFor(x => x.Options).NotNull().Must(o => o.Count <= 12 && o.Select(x => x.MatchCount).Distinct().Count() == o.Count)
            .WithMessage("Each package size once, up to 12 sizes.");
        RuleForEach(x => x.Options).ChildRules(o =>
        {
            o.RuleFor(x => x.MatchCount).InclusiveBetween(1, 12).WithMessage("A package has 1 to 12 matches.");
            o.RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
        });
    }
}

public sealed class RejectOptionRequestValidator : AbstractValidator<RejectOptionRequest>
{
    public RejectOptionRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Tell the partner why.").Length(5, 400);
    }
}
