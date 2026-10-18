using FluentValidation;
using SuperApp.Application.Abstractions.Documents;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Partners;

/// <summary>
/// The venue's bookings (API §3.4): list and Excel export (everyone in the business), walk-ins and no-shows (desk
/// staff), venue cancellation with a full refund (owner or manager).
/// </summary>
public interface IPartnerBookingsService
{
    Task<PagedResult<PartnerBookingDto>> ListAsync(PartnerBookingsQuery query, CancellationToken ct);

    Task<(byte[] File, string FileName)> ExportAsync(PartnerBookingsQuery query, CancellationToken ct);

    Task<PartnerBookingDto> WalkInAsync(WalkInRequest request, CancellationToken ct);

    Task<PartnerBookingDto> NoShowAsync(string code, CancellationToken ct);

    Task<PartnerBookingDto> CancelAsync(string code, VenueCancelRequest request, CancellationToken ct);
}

public sealed class PartnerBookingsService(
    IPartnerAccess access,
    IPartnerBookingsRepository repository,
    IBookingRepository bookings,
    BookingPlanner planner,
    IUnitOfWork unitOfWork,
    IExcelExporter excel,
    INotifier notifier,
    IAuditLogger audit,
    IClock clock,
    IValidator<PartnerBookingsQuery> queryRules,
    IValidator<WalkInRequest> walkInRules,
    IValidator<VenueCancelRequest> cancelRules,
    Waitlist.IWaitlistService waitlist) : IPartnerBookingsService
{
    private const int MaxExportRows = 5000;

    public async Task<PagedResult<PartnerBookingDto>> ListAsync(PartnerBookingsQuery query, CancellationToken ct)
    {
        await queryRules.ValidateAndThrowAsync(query, ct);
        var scope = await access.ViewAsync(ct);
        return await repository.SearchAsync(scope.PartnerId, query, Today(), ct);
    }

    public async Task<(byte[] File, string FileName)> ExportAsync(PartnerBookingsQuery query, CancellationToken ct)
    {
        await queryRules.ValidateAndThrowAsync(query, ct);
        var scope = await access.ViewAsync(ct);
        var rows = await repository.AllAsync(scope.PartnerId, query, Today(), MaxExportRows + 1, ct);
        if (rows.Count > MaxExportRows)
        {
            throw new BusinessRuleException($"More than {MaxExportRows:N0} bookings. Choose a shorter date range.", "too_many_rows");
        }
        var file = excel.Build(PartnerBookingsExcel.Dashboard(scope.BusinessName, query, rows), PartnerBookingsExcel.Sheet(rows));
        await audit.LogAsync(AuditSystem.Partner, "bookings.exported", $"{rows.Count} bookings exported", ct: ct);
        return (file, $"bookings-{Today():yyyy-MM-dd}.xlsx");
    }

    public async Task<PartnerBookingDto> WalkInAsync(WalkInRequest request, CancellationToken ct)
    {
        await walkInRules.ValidateAndThrowAsync(request, ct);
        var scope = await access.DeskAsync(ct);
        var pitch = await repository.PitchAsync(scope.PartnerId, request.PitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        var plan = await planner.PlanAsync(pitch.Slug, request.Date, request.Hour, request.Duration, ct, allowStarted: true);
        var booking = new Booking
        {
            Code = await bookings.NewCodeAsync(ct), PitchId = plan.Pitch.Id, Source = BookingSource.WalkIn,
            WalkInName = request.Name.Trim(), WalkInPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            Date = plan.Lines[0].Date, StartHour = (byte)plan.Lines[0].CalendarHour, Duration = (byte)request.Duration,
            PitchAmount = plan.PitchAmount, Total = plan.PitchAmount, CommissionRate = 0, PayAtVenue = true,
            PolicyKey = plan.Pitch.PolicyKey, Status = BookingStatus.Confirmed,
        };
        await bookings.SaveAsync(booking, plan.Slots, ct);
        if (!scope.IsOwner)
        {
            await notifier.NotifyUserAsync(scope.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings,
                $"Walk-in booked at {pitch.Name}", $"{booking.WalkInName} · {booking.Date:ddd d MMM} {booking.StartHour:00}:00 · {booking.Total:0} EGP", "#/dash/bookings"), ct);
        }
        await audit.LogAsync(AuditSystem.Partner, "booking.walk_in", $"Walk-in {booking.Code} at {pitch.Name}", "Booking", booking.Code, ct: ct);
        return (await repository.GetAsync(scope.PartnerId, booking.Code, ct))!;
    }

    public async Task<PartnerBookingDto> NoShowAsync(string code, CancellationToken ct)
    {
        var scope = await access.DeskAsync(ct);
        var booking = await ConfirmedAsync(scope, code, ct);
        if (KickOff(booking) > CairoTime.Now(clock))
        {
            throw new BusinessRuleException("The game hasn’t started yet. Mark a no-show after kick-off.", "not_started");
        }
        booking.Status = BookingStatus.NoShow;
        await unitOfWork.SaveChangesAsync(ct);
        await TellCustomerAsync(booking, "Marked as a no-show", "The venue says nobody came for this booking. Contact support if that’s wrong.", NotificationTone.Warning, ct);
        await audit.LogAsync(AuditSystem.Partner, "booking.no_show", $"Booking {booking.Code} marked no-show", "Booking", booking.Code, ct: ct);
        return (await repository.GetAsync(scope.PartnerId, code, ct))!;
    }

    public async Task<PartnerBookingDto> CancelAsync(string code, VenueCancelRequest request, CancellationToken ct)
    {
        await cancelRules.ValidateAndThrowAsync(request, ct);
        var scope = await access.ManageAsync(ct);
        var booking = await ConfirmedAsync(scope, code, ct);
        if (KickOff(booking) <= CairoTime.Now(clock))
        {
            throw new BusinessRuleException("This game has already started and can’t be cancelled.", "already_started");
        }
        booking.Status = BookingStatus.CancelledByVenue;
        booking.CancelledAt = clock.UtcNow;
        booking.RefundAmount = booking.PayAtVenue ? 0 : booking.Total;
        await unitOfWork.SaveChangesAsync(ct);
        await bookings.ReleaseSlotsAsync(booking.Id, ct);
        var info = await bookings.PitchInfoAsync(booking.PitchId, ct);
        await waitlist.HoursFreedAsync(booking, info.Name, info.Slug, ct);
        await TellCustomerAsync(booking, "The venue cancelled your booking",
            $"{request.Reason.Trim()} · {(booking.RefundAmount > 0 ? $"full refund {booking.RefundAmount:0} EGP" : "nothing was paid in the app")}", NotificationTone.Danger, ct);
        await audit.LogAsync(AuditSystem.Partner, "booking.cancelled_by_venue", $"Booking {booking.Code} cancelled by the venue: {request.Reason.Trim()}", "Booking", booking.Code, ct: ct);
        return (await repository.GetAsync(scope.PartnerId, code, ct))!;
    }

    private async Task<Booking> ConfirmedAsync(PartnerScope scope, string code, CancellationToken ct)
    {
        var booking = await repository.FindAsync(scope.PartnerId, code, ct) ?? throw new NotFoundException("Booking not found.");
        return booking.Status == BookingStatus.Confirmed ? booking
            : throw new BusinessRuleException($"This booking is {booking.Status}; only confirmed bookings can be changed.", "wrong_status");
    }

    private async Task TellCustomerAsync(Booking b, string title, string body, NotificationTone tone, CancellationToken ct)
    {
        if (b.CustomerId is { } customer)
        {
            await notifier.NotifyUserAsync(customer, new NotificationMessage(NotificationCategory.Bookings, title, $"{b.Code} · {body}", $"#/booking/{b.Code}", tone), ct);
        }
    }

    private static DateTime KickOff(Booking b) => b.Date.ToDateTime(new TimeOnly(b.StartHour, 0));

    private DateOnly Today() => DateOnly.FromDateTime(CairoTime.Now(clock));
}

public sealed class PartnerBookingsQueryValidator : AbstractValidator<PartnerBookingsQuery>
{
    private static readonly string[] Statuses = ["upcoming", "past", "cancelled", "noshow", "all"];

    public PartnerBookingsQueryValidator()
    {
        RuleFor(x => x.Status).Must(s => s is null || Statuses.Contains(s)).WithMessage("Status is upcoming, past, cancelled, noshow or all.");
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From is not null && x.To is not null).WithMessage("The end date can’t be before the start date.");
        RuleFor(x => x.Q).MaximumLength(60);
    }
}

public sealed class WalkInRequestValidator : AbstractValidator<WalkInRequest>
{
    public WalkInRequestValidator()
    {
        RuleFor(x => x.PitchId).GreaterThan(0);
        RuleFor(x => x.Hour).InclusiveBetween(0, 29);
        RuleFor(x => x.Duration).InclusiveBetween(1, 4).WithMessage("Book 1 to 4 hours in a row.");
        RuleFor(x => x.Name).NotEmpty().Length(2, 80);
        RuleFor(x => x.Phone!).EgyptianMobile().When(x => !string.IsNullOrWhiteSpace(x.Phone));
    }
}

public sealed class VenueCancelRequestValidator : AbstractValidator<VenueCancelRequest>
{
    public VenueCancelRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Tell the player why (they will see it).").Length(3, 300);
    }
}
