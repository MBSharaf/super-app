using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Bookings;

/// <summary>A customer's own bookings (API §3.3): list, page with QR, refund preview, cancel. Others' bookings answer 404.</summary>
public interface IMyBookingsService
{
    Task<PagedResult<BookingResponse>> MineAsync(MyBookingsQuery query, CancellationToken ct);

    Task<BookingResponse> GetAsync(string code, CancellationToken ct);

    Task<RefundPreview> RefundPreviewAsync(string code, CancellationToken ct);

    Task<BookingResponse> CancelAsync(string code, CancellationToken ct);

    Task<byte[]> QrPngAsync(string code, CancellationToken ct);
}

public sealed class MyBookingsService(
    IBookingRepository bookings,
    IUnitOfWork unitOfWork,
    IQrTokens qr,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IClock clock) : IMyBookingsService
{
    private static readonly string[] Tabs = ["upcoming", "past", "cancelled"];

    public async Task<PagedResult<BookingResponse>> MineAsync(MyBookingsQuery query, CancellationToken ct)
    {
        var tab = Tabs.Contains(query.Tab) ? query.Tab! : "upcoming";
        var page = new PageRequest(query.Page, query.PageSize);
        var result = await bookings.MineAsync(Me(), tab, DateOnly.FromDateTime(CairoTime.Now(clock)), page.SafePage, page.SafePageSize, ct);
        var items = new List<BookingResponse>();
        foreach (var b in result.Items)
        {
            items.Add(await ToResponseAsync(b, withQr: false, ct));
        }
        return new PagedResult<BookingResponse>(items, result.Page, result.PageSize, result.Total);
    }

    public async Task<BookingResponse> GetAsync(string code, CancellationToken ct) =>
        await ToResponseAsync(await OwnAsync(code, ct), withQr: true, ct);

    public async Task<RefundPreview> RefundPreviewAsync(string code, CancellationToken ct)
    {
        var booking = await OwnAsync(code, ct);
        var pitch = await bookings.PitchAsync((await bookings.PitchInfoAsync(booking.PitchId, ct)).Slug, ct);
        return Preview(booking, pitch?.FullRefundHours ?? 6, pitch?.HalfRefundHours ?? 0);
    }

    public async Task<BookingResponse> CancelAsync(string code, CancellationToken ct)
    {
        var booking = await OwnAsync(code, ct);
        var preview = await RefundPreviewAsync(code, ct);
        if (!preview.CanCancel)
        {
            throw new BusinessRuleException("This booking can no longer be cancelled.", "cannot_cancel");
        }
        booking.Status = BookingStatus.CancelledByCustomer;
        booking.CancelledAt = clock.UtcNow;
        booking.RefundAmount = preview.RefundAmount;
        await unitOfWork.SaveChangesAsync(ct);
        await bookings.ReleaseSlotsAsync(booking.Id, ct);
        await TellAboutCancelAsync(booking, ct);
        return await ToResponseAsync(booking, withQr: false, ct);
    }

    public async Task<byte[]> QrPngAsync(string code, CancellationToken ct)
    {
        var booking = await OwnAsync(code, ct);
        return qr.Png(qr.Create(booking.Code));
    }

    /// <summary>Refund by the policy: full before its first limit, half before the second, nothing after.</summary>
    private RefundPreview Preview(Booking b, int fullHours, int halfHours)
    {
        var kickOff = b.Date.ToDateTime(new TimeOnly(b.StartHour, 0));
        var hoursLeft = Math.Round((kickOff - CairoTime.Now(clock)).TotalHours, 1);
        var open = b.Status == BookingStatus.Confirmed && hoursLeft > 0;
        var percent = !open ? 0 : hoursLeft >= fullHours ? 100 : hoursLeft >= halfHours ? 50 : 0;
        return new RefundPreview(b.Code, hoursLeft, percent, Math.Round(b.Total * percent / 100, 2), open);
    }

    private async Task TellAboutCancelAsync(Booking b, CancellationToken ct)
    {
        var info = await bookings.PitchInfoAsync(b.PitchId, ct);
        var owner = (await bookings.PitchAsync(info.Slug, ct))?.OwnerUserId;
        await notifier.NotifyUserAsync(b.CustomerId!.Value, new NotificationMessage(NotificationCategory.Bookings,
            $"Booking cancelled: {info.Name}", $"{b.Code} · refund {b.RefundAmount:0} EGP", $"#/booking/{b.Code}", NotificationTone.Warning), ct);
        if (owner is { } ownerId)
        {
            await notifier.NotifyUserAsync(ownerId, new NotificationMessage(NotificationCategory.Bookings,
                $"A booking was cancelled at {info.Name}", $"{b.Code} · the hour is free again", "#/dash/bookings", NotificationTone.Info), ct);
        }
        await audit.LogAsync(AuditSystem.Site, "booking.cancelled", $"Booking {b.Code} cancelled by the customer, refund {b.RefundAmount}", "Booking", b.Code, ct: ct);
    }

    private async Task<Booking> OwnAsync(string code, CancellationToken ct)
    {
        var booking = await bookings.FindAsync(code, ct);
        return booking is not null && booking.CustomerId == Me() ? booking : throw new NotFoundException("This booking does not exist.");
    }

    private async Task<BookingResponse> ToResponseAsync(Booking b, bool withQr, CancellationToken ct)
    {
        var info = await bookings.PitchInfoAsync(b.PitchId, ct);
        var token = withQr && b.Status == BookingStatus.Confirmed ? qr.Create(b.Code) : null;
        var package = b.PackageId is { } id ? await bookings.PackageCodeAsync(id, ct) : null;
        return BookingMapper.ToResponse(b, info.Slug, info.Name, info.Address, token, package);
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}
