using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>
/// The partner's pitches (API §3.2). A new pitch waits for the admin's review; a live pitch stays live when edited;
/// editing a rejected pitch sends it for review again. Close / reopen hides or shows a live pitch for new bookings.
/// </summary>
public interface IPartnerPitchesService
{
    Task<IReadOnlyList<MyPitchDto>> ListAsync(CancellationToken ct);

    Task<PitchEditDto> GetAsync(long id, CancellationToken ct);

    Task<PitchEditDto> CreateAsync(PitchRequest request, CancellationToken ct);

    Task<PitchEditDto> UpdateAsync(long id, PitchRequest request, CancellationToken ct);

    Task<PitchEditDto> CloseAsync(long id, CancellationToken ct);

    Task<PitchEditDto> ReopenAsync(long id, CancellationToken ct);
}

public sealed class PartnerPitchesService(
    IPartnerAccess access,
    IPartnerRepository partners,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IValidator<PitchRequest> rules) : IPartnerPitchesService
{
    public async Task<IReadOnlyList<MyPitchDto>> ListAsync(CancellationToken ct) =>
        await partners.PitchesAsync((await access.ViewAsync(ct)).PartnerId, ct);

    public async Task<PitchEditDto> GetAsync(long id, CancellationToken ct) =>
        await partners.PitchForEditAsync((await access.ViewAsync(ct)).PartnerId, id, ct) ?? throw new NotFoundException("Pitch not found.");

    public async Task<PitchEditDto> CreateAsync(PitchRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        var pitch = new Pitch { Slug = await partners.FreeSlugAsync(request.Name, ct), Status = PitchStatus.WaitingForReview };
        await ApplyAsync(scope, pitch, request, ct);
        partners.AddPitch(pitch);
        await unitOfWork.SaveChangesAsync(ct);
        await SentForReviewAsync(scope, pitch, "pitch.created", ct);
        return await GetAsync(pitch.Id, ct);
    }

    public async Task<PitchEditDto> UpdateAsync(long id, PitchRequest request, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        var version = Version(request.RowVersion);
        var scope = await access.ManageAsync(ct);
        var pitch = await partners.PitchAsync(scope.PartnerId, id, ct) ?? throw new NotFoundException("Pitch not found.");
        partners.ExpectVersion(pitch, version);
        await ApplyAsync(scope, pitch, request, ct);
        var resubmitted = pitch.Status == PitchStatus.Rejected;
        if (resubmitted)
        {
            pitch.Status = PitchStatus.WaitingForReview;
            pitch.RejectReason = null;
        }
        await unitOfWork.SaveChangesAsync(ct);
        if (resubmitted)
        {
            await SentForReviewAsync(scope, pitch, "pitch.resubmitted", ct);
        }
        else
        {
            await audit.LogAsync(AuditSystem.Partner, "pitch.updated", $"{pitch.Name} edited", "Pitch", pitch.Slug, ct: ct);
        }
        return await GetAsync(pitch.Id, ct);
    }

    public Task<PitchEditDto> CloseAsync(long id, CancellationToken ct) =>
        SwitchAsync(id, PitchStatus.Live, PitchStatus.ClosedByOwner, "Only a live pitch can be closed for booking.", ct);

    public Task<PitchEditDto> ReopenAsync(long id, CancellationToken ct) =>
        SwitchAsync(id, PitchStatus.ClosedByOwner, PitchStatus.Live, "Only a pitch you closed can be reopened. A pitch closed by Super App reopens after its review.", ct);

    private async Task<PitchEditDto> SwitchAsync(long id, PitchStatus from, PitchStatus to, string notAllowed, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        var pitch = await partners.PitchAsync(scope.PartnerId, id, ct) ?? throw new NotFoundException("Pitch not found.");
        if (pitch.Status != from)
        {
            throw new BusinessRuleException(notAllowed, "wrong_status");
        }
        pitch.Status = to;
        await unitOfWork.SaveChangesAsync(ct);
        var open = to == PitchStatus.Live;
        await notifier.NotifyUserAsync(scope.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings,
            open ? $"{pitch.Name} is open for booking again" : $"{pitch.Name} is closed for new bookings",
            open ? "Players can book it on Super App." : "Bookings already made stay as they are.", "#/dash/pitches", open ? NotificationTone.Success : NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Partner, open ? "pitch.reopened" : "pitch.closed", $"{pitch.Name} {(open ? "reopened" : "closed")} for booking", "Pitch", pitch.Slug, ct: ct);
        return await GetAsync(pitch.Id, ct);
    }

    /// <summary>Copies the form onto the pitch after checking the venue, policy and facilities belong to the lists.</summary>
    private async Task ApplyAsync(PartnerScope scope, Pitch pitch, PitchRequest r, CancellationToken ct)
    {
        _ = await partners.VenueAsync(scope.PartnerId, r.VenueId, ct) ?? throw new BadRequestException("Choose one of your venues.", "unknown_venue");
        var policyId = await partners.PolicyIdAsync(r.PolicyKey, ct) ?? throw new BadRequestException("Choose a cancellation policy from the list.", "unknown_policy");
        var facilities = await partners.FacilityIdsAsync(r.Facilities, ct);
        if (facilities.Count != r.Facilities.Distinct().Count())
        {
            throw new BadRequestException("Choose facilities from the list.", "unknown_facility");
        }
        pitch.VenueId = r.VenueId;
        pitch.Name = r.Name.Trim();
        pitch.Format = (byte)r.Format;
        pitch.Surface = Enum.Parse<PitchSurface>(r.Surface);
        pitch.IsIndoor = r.IsIndoor;
        pitch.PricePerHour = r.PricePerHour;
        pitch.PeakPricePerHour = r.PeakPricePerHour;
        pitch.PeakFromHour = r.PeakPricePerHour is null ? null : (byte?)r.PeakFromHour;
        pitch.DescriptionEn = string.IsNullOrWhiteSpace(r.DescriptionEn) ? null : r.DescriptionEn.Trim();
        pitch.DescriptionAr = string.IsNullOrWhiteSpace(r.DescriptionAr) ? null : r.DescriptionAr.Trim();
        pitch.CancellationPolicyId = policyId;
        pitch.AllowCash = r.AllowCash;
        PitchChanges.SyncFacilities(pitch, facilities.Values.ToHashSet());
        PitchChanges.SyncHours(pitch, r.OpeningHours);
    }

    private async Task SentForReviewAsync(PartnerScope scope, Pitch pitch, string action, CancellationToken ct)
    {
        await notifier.NotifyStaffAsync("approvals.pitches", new NotificationMessage(NotificationCategory.System,
            $"New pitch to review: {pitch.Name}", scope.BusinessName, "#/admin/approvals", NotificationTone.Info), ct);
        await notifier.NotifyUserAsync(scope.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings,
            $"{pitch.Name} was sent for review", "Super App checks new pitches within 2 working days. You’ll get a notification.", "#/dash/pitches"), ct);
        await audit.LogAsync(AuditSystem.Partner, action, $"{pitch.Name} sent for review", "Pitch", pitch.Slug, ct: ct);
    }

    private static byte[] Version(string? rowVersion)
    {
        try
        {
            return string.IsNullOrEmpty(rowVersion) ? throw new FormatException() : Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            throw new BadRequestException("Reload the pitch and try again (rowVersion missing).", "row_version");
        }
    }
}
