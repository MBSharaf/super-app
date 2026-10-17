using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Bookings;

/// <summary>Holding and booking hours (API §3.3). Payments arrive in Phase 5: until then the booking is confirmed straight away.</summary>
public interface IBookingsService
{
    Task<HoldResponse> HoldAsync(HoldRequest request, CancellationToken ct);

    Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken ct);
}

public sealed class BookingsService(
    BookingPlanner planner,
    IBookingRepository bookings,
    ILegalRepository legal,
    ISettingsReader settings,
    IQrTokens qr,
    IFeatureFlags flags,
    INotifier notifier,
    IAuditLogger audit,
    ICurrentUser currentUser,
    IRequestInfo requestInfo,
    IValidator<HoldRequest> holdRules,
    IValidator<CreateBookingRequest> createRules) : IBookingsService
{
    public async Task<HoldResponse> HoldAsync(HoldRequest request, CancellationToken ct)
    {
        await holdRules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync(KillSwitches.Bookings, ct);
        var plan = await planner.PlanAsync(request.PitchSlug, request.Date, request.Hour, request.Duration, ct);
        var expires = await bookings.HoldAsync(plan.Pitch.Id, plan.Slots, Me(), ct);
        return new HoldResponse(expires, plan.Lines, plan.PitchAmount, plan.ServiceFee, plan.Total);
    }

    public async Task<BookingResponse> CreateAsync(CreateBookingRequest request, CancellationToken ct)
    {
        await createRules.ValidateAndThrowAsync(request, ct);
        await flags.EnsureOnAsync(KillSwitches.Bookings, ct);
        var me = Me();
        var repeated = await RepeatedAsync(request, me, ct);
        if (repeated is not null)
        {
            return repeated;
        }
        var plan = await planner.PlanAsync(request.PitchSlug, request.Date, request.Hour, request.Duration, ct);
        if (request.PayAtVenue && !plan.Pitch.AllowCash)
        {
            throw new BusinessRuleException("This pitch takes online payment only.", "cash_not_allowed");
        }
        var booking = await NewBookingAsync(plan, request, me, ct);
        await bookings.SaveAsync(booking, plan.Slots, ct);
        await AfterBookingAsync(booking, plan, ct);
        return BookingMapper.ToResponse(booking, plan.Pitch.Slug, plan.Pitch.Name, plan.Pitch.Address, qr.Create(booking.Code));
    }

    /// <summary>A repeated request (double tap, retry) gets the booking it already made, before any hour check.</summary>
    private async Task<BookingResponse?> RepeatedAsync(CreateBookingRequest request, Guid me, CancellationToken ct)
    {
        var pitch = await bookings.PitchAsync(request.PitchSlug, ct);
        if (pitch is null)
        {
            return null;
        }
        var date = request.Hour >= 24 ? request.Date.AddDays(1) : request.Date;
        var same = await bookings.RecentSameAsync(me, pitch.Id, date, request.Hour % 24, request.Duration, ct);
        return same is null ? null : BookingMapper.ToResponse(same, pitch.Slug, pitch.Name, pitch.Address, qr.Create(same.Code));
    }

    private async Task<Booking> NewBookingAsync(BookingPlan plan, CreateBookingRequest request, Guid me, CancellationToken ct)
    {
        var waiver = await legal.CurrentVersionIdAsync("waiver", ct);
        var commission = plan.Pitch.PartnerCommission ?? (await settings.DecimalAsync("commission.bookings", 0.10m, ct) * 100);
        var code = await bookings.NewCodeAsync(ct);
        return new Booking
        {
            Code = code, PitchId = plan.Pitch.Id, CustomerId = me, Source = BookingSource.App,
            Date = plan.Lines[0].Date, StartHour = (byte)plan.Lines[0].CalendarHour, Duration = (byte)request.Duration,
            PitchAmount = plan.PitchAmount, ServiceFee = plan.ServiceFee, Total = plan.Total, CommissionRate = commission,
            PayAtVenue = request.PayAtVenue, PolicyKey = plan.Pitch.PolicyKey, WaiverVersionId = waiver, Status = BookingStatus.Confirmed,
        };
    }

    /// <summary>Waiver acceptance, notifications to the customer and the pitch owner, audit.</summary>
    private async Task AfterBookingAsync(Booking booking, BookingPlan plan, CancellationToken ct)
    {
        if (booking.WaiverVersionId is { } waiver)
        {
            legal.AddAcceptance(waiver, booking.CustomerId!.Value, "booking", requestInfo.IpAddress);
        }
        var when = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{booking.Date:ddd d MMM} · {plan.Lines[0].Hour % 24:00}:00 · {booking.Duration} h");
        await notifier.NotifyUserAsync(booking.CustomerId!.Value, new NotificationMessage(NotificationCategory.Bookings,
            $"Booking confirmed: {plan.Pitch.Name}", $"{when} · {booking.Code}", $"#/booking/{booking.Code}", NotificationTone.Success), ct);
        await notifier.NotifyUserAsync(plan.Pitch.OwnerUserId, new NotificationMessage(NotificationCategory.Bookings,
            $"New booking at {plan.Pitch.Name}", $"{when} · {booking.Total:0} EGP", "#/dash/bookings", NotificationTone.Info), ct);
        await audit.LogAsync(AuditSystem.Site, "booking.created", $"Booking {booking.Code} at {plan.Pitch.Name}", "Booking", booking.Code, ct: ct);
    }

    private Guid Me() => currentUser.UserId ?? throw new UnauthorizedException();
}

public sealed class HoldRequestValidator : AbstractValidator<HoldRequest>
{
    public HoldRequestValidator()
    {
        RuleFor(x => x.PitchSlug).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Hour).InclusiveBetween(0, 29);
        RuleFor(x => x.Duration).InclusiveBetween(1, 4).WithMessage("Book 1 to 4 hours in a row.");
    }
}

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.PitchSlug).NotEmpty().MaximumLength(60);
        RuleFor(x => x.Hour).InclusiveBetween(0, 29);
        RuleFor(x => x.Duration).InclusiveBetween(1, 4).WithMessage("Book 1 to 4 hours in a row.");
        RuleFor(x => x.AcceptWaiver).Equal(true).WithMessage("Please accept the injury waiver to book.");
    }
}

/// <summary>Booking entity → the customer's view.</summary>
public static class BookingMapper
{
    public static BookingResponse ToResponse(Booking b, string slug, string name, string address, string? qrToken) => new(
        b.Code, slug, name, address, b.Date, b.StartHour, b.Duration, b.PitchAmount, b.ServiceFee, b.Total, b.PayAtVenue,
        b.Status.ToString(), b.PolicyKey, b.RefundAmount, b.CreatedAt, b.CancelledAt, qrToken);
}
