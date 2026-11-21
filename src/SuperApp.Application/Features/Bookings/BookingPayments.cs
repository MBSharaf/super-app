using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Payments;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Bookings;

/// <summary>A confirmed booking: the customer and the pitch owner are told, and the audit line (→ webhook booking.created).</summary>
public sealed class BookingNotices(INotifier notifier, IAuditLogger audit, ICustomerContacts contacts)
{
    public async Task ConfirmedAsync(Booking booking, string pitchName, Guid ownerUserId, CancellationToken ct)
    {
        var when = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{booking.Date:ddd d MMM} · {booking.StartHour:00}:{booking.StartMinute:00} · {(booking.Minutes > 0 ? $"{booking.Minutes} min" : $"{booking.Duration} h")}");
        var extras = booking.ExtrasAmount > 0 ? $" · rackets and balls {booking.ExtrasAmount:0} EGP at the club" : "";
        await notifier.NotifyUserAsync(booking.CustomerId!.Value, new NotificationMessage(NotificationCategory.Bookings,
            $"Booking confirmed: {pitchName}", (booking.BalanceDue > 0
                ? $"{when} · {booking.Code} · deposit {booking.Deposit:0} EGP paid · {booking.BalanceDue:0} EGP at the pitch"
                : $"{when} · {booking.Code} · {booking.Deposit:0} EGP paid in full · nothing to pay at the pitch") + extras,
            $"#/booking/{booking.Code}", NotificationTone.Success), ct);
        await notifier.NotifyUserAsync(ownerUserId, new NotificationMessage(NotificationCategory.Bookings,
            $"New booking at {pitchName}", (booking.BalanceDue > 0
                ? $"{when} · deposit {booking.Deposit:0} EGP paid in the app · collect {booking.BalanceDue:0} EGP at the pitch"
                : $"{when} · paid in full in the app ({booking.Deposit:0} EGP) · nothing to collect · Super Malaab pays your share after the match") + extras, "#/dash/bookings", NotificationTone.Info), ct);
        await audit.LogAsync(AuditSystem.Site, "booking.created", $"Booking {booking.Code} at {pitchName}", "Booking", booking.Code,
            Details(booking, pitchName, await contacts.GetAsync(booking.CustomerId.Value, ct)), ct);
    }

    /// <summary>What the activity line (and the booking.created webhook for n8n) carries, with the customer's contact (owner's request).</summary>
    private static object Details(Booking b, string pitchName, CustomerContact customer) => new
    {
        b.Code, Pitch = pitchName, Date = b.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), b.StartHour, b.Duration,
        Sport = b.Minutes > 0 ? "Padel" : "Football", b.StartMinute, b.Minutes, b.Rackets, b.Balls, b.ExtrasAmount,
        b.PitchAmount, b.DiscountAmount, b.ServiceFee, b.Total, b.Deposit, b.BalanceDue, PaymentMode = b.PaymentMode.ToString(), b.PayAtVenue, Source = b.Source.ToString(), b.CreatedAt,
        Customer = new { customer.Name, customer.Email, customer.Phone },
    };
}

/// <summary>The gateway confirmed (or refused) a booking's payment.</summary>
public sealed class BookingPaymentListener(
    IBookingRepository bookings, BookingNotices notices, Money.ILedger ledger, INotifier notifier, IUnitOfWork unitOfWork, LatePaymentRefunds late)
    : IPaymentListener
{
    public PaymentPurpose Purpose => PaymentPurpose.Booking;

    public async Task PaidAsync(Payment payment, CancellationToken ct)
    {
        var booking = await bookings.ByPaymentAsync(payment.Id, ct);
        if (booking is null || booking.Status == BookingStatus.Confirmed)
        {
            return;
        }
        if (booking.Status != BookingStatus.AwaitingPayment)
        {
            await late.RefundAsync(payment, booking, ct);       // paid after the 15 minutes: the hours are gone, the money goes back
            return;
        }
        booking.Status = BookingStatus.Confirmed;
        booking.Payment = payment;
        await ledger.BookingPaidAsync(booking, ct);
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

/// <summary>The gateway confirmed a weekly package: every match is confirmed and booked into the ledger.</summary>
public sealed class PackagePaymentListener(IBookingRepository bookings, Money.ILedger ledger, INotifier notifier, IUnitOfWork unitOfWork, LatePaymentRefunds late)
    : IPaymentListener
{
    public PaymentPurpose Purpose => PaymentPurpose.Package;

    public async Task PaidAsync(Payment payment, CancellationToken ct)
    {
        var all = await bookings.AllByPaymentAsync(payment.Id, ct);
        if (all.Count > 0 && all.All(b => b.Status is not (BookingStatus.AwaitingPayment or BookingStatus.Confirmed)))
        {
            await late.RefundAsync(payment, all[0], ct);        // the whole package expired before the money came
            return;
        }
        var matches = all.Where(b => b.Status == BookingStatus.AwaitingPayment).ToList();
        foreach (var b in matches)
        {
            b.Status = BookingStatus.Confirmed;
            b.Payment = payment;
            await ledger.BookingPaidAsync(b, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
        if (matches.Count > 0 && payment.PayerUserId is { } payer)
        {
            await notifier.NotifyUserAsync(payer, new NotificationMessage(NotificationCategory.Bookings, "Package confirmed",
                $"{matches.Count} matches · {payment.Amount:0} EGP", "#/bookings", NotificationTone.Success), ct);
        }
    }

    public Task FailedAsync(Payment payment, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// The gateway took the money after the booking had already been called off (paid after the 15 minutes, the hours freed
/// for others). The whole amount goes straight back to the card; the customer and the refunds team are told. If the
/// gateway refuses the refund, it is left Requested for the team (<c>refunds.issue</c>) to finish by hand.
/// </summary>
public sealed class LatePaymentRefunds(IPaymentGateway gateway, Money.ILedger ledger, INotifier notifier, IAuditLogger audit, IUnitOfWork unitOfWork)
{
    public async Task RefundAsync(Payment payment, Booking booking, CancellationToken ct)
    {
        string? refundRef;
        try
        {
            refundRef = await gateway.RefundAsync(payment.ProviderRef ?? payment.Code, payment.Amount, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            refundRef = null;
        }
        await ledger.LatePaymentRefundAsync(payment, booking.Id, refundRef, ct);
        await unitOfWork.SaveChangesAsync(ct);
        var done = refundRef is not null;
        await notifier.NotifyUserAsync(payment.PayerUserId!.Value, new NotificationMessage(NotificationCategory.Bookings,
            done ? "Late payment refunded" : "Late payment: refund on its way",
            $"Booking {booking.Code} had expired before your payment of {payment.Amount:0} EGP arrived. "
                + (done ? "The full amount was refunded to your card." : "Our team will refund the full amount to your card within 2 working days."),
            $"#/booking/{booking.Code}", done ? NotificationTone.Info : NotificationTone.Warning), ct);
        await notifier.NotifyStaffAsync("refunds.issue", new NotificationMessage(NotificationCategory.Bookings,
            done ? "Late payment refunded automatically" : "Late payment needs a manual refund",
            $"Payment {payment.Code} ({payment.Amount:0} EGP) arrived after booking {booking.Code} expired.",
            "#/admin/finance-approvals", done ? NotificationTone.Info : NotificationTone.Warning), ct);
        await audit.LogAsync(AuditSystem.Site, done ? "payment.late_refunded" : "payment.late_refund_pending",
            $"Payment {payment.Code} arrived after booking {booking.Code} expired: {payment.Amount:0.##} EGP {(done ? "refunded" : "to refund")}",
            "Payment", payment.Code, ct: ct);
    }
}
