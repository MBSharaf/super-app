using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>
/// DELETE /partner/pitches/{id} (owner only): every game still to come is cancelled by the venue with a full refund
/// and its player is told; then the pitch is removed (soft delete, history kept).
/// </summary>
public interface IPartnerPitchRemovalService
{
    Task<PitchRemovedDto> RemoveAsync(long pitchId, CancellationToken ct);
}

public sealed class PartnerPitchRemovalService(
    IPartnerAccess access,
    IPartnerRepository partners,
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    IAuditLogger audit,
    IClock clock) : IPartnerPitchRemovalService
{
    public async Task<PitchRemovedDto> RemoveAsync(long pitchId, CancellationToken ct)
    {
        var scope = await access.ManageAsync(ct);
        if (!scope.IsOwner)
        {
            throw new ForbiddenException("Only the owner can delete a pitch.", "owner_only");
        }
        var pitch = await partners.PitchAsync(scope.PartnerId, pitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        var now = CairoTime.Now(clock);
        var cancelled = (await partners.UpcomingBookingsAsync(pitchId, DateOnly.FromDateTime(now), ct))
            .Where(b => b.Date.ToDateTime(new TimeOnly(b.StartHour, 0)) > now).ToList();
        foreach (var b in cancelled)
        {
            b.Status = BookingStatus.CancelledByVenue;
            b.CancelledAt = clock.UtcNow;
            b.RefundAmount = b.PayAtVenue ? 0 : b.Total;
        }
        pitch.IsDeleted = true;
        pitch.DeletedAt = clock.UtcNow;
        await unitOfWork.SaveChangesAsync(ct);
        foreach (var b in cancelled)
        {
            await bookings.ReleaseSlotsAsync(b.Id, ct);
        }
        await TellAsync(scope, pitch, cancelled, ct);
        return new PitchRemovedDto(pitch.Id, cancelled.Count, cancelled.Sum(b => b.RefundAmount));
    }

    private async Task TellAsync(PartnerScope scope, Pitch pitch, List<Booking> cancelled, CancellationToken ct)
    {
        foreach (var b in cancelled.Where(b => b.CustomerId is not null))
        {
            await notifier.NotifyUserAsync(b.CustomerId!.Value, new NotificationMessage(NotificationCategory.Bookings, "The venue cancelled your booking",
                $"{b.Code} · {pitch.Name} is no longer on Super App · full refund {b.RefundAmount:0} EGP", $"#/booking/{b.Code}", NotificationTone.Danger), ct);
        }
        await notifier.NotifyUserAsync(scope.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings, $"{pitch.Name} was deleted",
            cancelled.Count == 0 ? "No upcoming bookings were affected." : $"{cancelled.Count} upcoming booking(s) cancelled and refunded in full.", "#/dash/pitches", NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Partner, "pitch.deleted", $"{pitch.Name} deleted; {cancelled.Count} booking(s) cancelled", "Pitch", pitch.Slug, ct: ct);
    }
}
