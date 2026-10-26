using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Payments;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Bookings;

/// <summary>A confirmed booking: the customer and the pitch owner are told, and the audit line (→ webhook booking.created).</summary>
public sealed class BookingNotices(INotifier notifier, IAuditLogger audit)
{
    public async Task ConfirmedAsync(Booking booking, string pitchName, Guid ownerUserId, CancellationToken ct)
    {
        var when = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{booking.Date:ddd d MMM} · {booking.StartHour:00}:00 · {booking.Duration} h");
        await notifier.NotifyUserAsync(booking.CustomerId!.Value, new NotificationMessage(NotificationCategory.Bookings,
            $"Booking confirmed: {pitchName}", $"{when} · {booking.Code}", $"#/booking/{booking.Code}", NotificationTone.Success), ct);
        await notifier.NotifyUserAsync(ownerUserId, new NotificationMessage(NotificationCategory.Bookings,
            $"New booking at {pitchName}", $"{when} · {booking.Total:0} EGP", "#/dash/bookings", NotificationTone.Info), ct);
        await audit.LogAsync(AuditSystem.Site, "booking.created", $"Booking {booking.Code} at {pitchName}", "Booking", booking.Code, ct: ct);
    }
}

/// <summary>The gateway confirmed (or refused) a booking's payment.</summary>
public sealed class BookingPaymentListener(IBookingRepository bookings, BookingNotices notices, INotifier notifier, IUnitOfWork unitOfWork) : IPaymentListener
{
    public PaymentPurpose Purpose => PaymentPurpose.Booking;

    public async Task PaidAsync(Payment payment, CancellationToken ct)
    {
        var booking = await bookings.ByPaymentAsync(payment.Id, ct);
        if (booking is null || booking.Status != BookingStatus.AwaitingPayment)
        {
            return;
        }
        booking.Status = BookingStatus.Confirmed;
        await unitOfWork.SaveChangesAsync(ct);
        var info = await bookings.PitchInfoAsync(booking.PitchId, ct);
        var pitch = await bookings.PitchAsync(info.Slug, ct);
        await notices.ConfirmedAsync(booking, info.Name, pitch?.OwnerUserId ?? Guid.Empty, ct);
    }

    public async Task FailedAsync(Payment payment, CancellationToken ct)
    {
        var booking = await bookings.ByPaymentAsync(payment.Id, ct);
        if (booking?.CustomerId is { } customer && booking.Status == BookingStatus.AwaitingPayment)
        {
            await notifier.NotifyUserAsync(customer, new NotificationMessage(NotificationCategory.Bookings, "Payment did not go through",
                $"Booking {booking.Code}: try again within 15 minutes to keep the hours.", $"#/booking/{booking.Code}", NotificationTone.Warning), ct);
        }
    }
}
