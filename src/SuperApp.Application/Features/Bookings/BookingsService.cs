using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Bookings;

/// <summary>Holding and booking hours (API §3.3). Online bookings are paid through <see cref="Payments.IPaymentsService"/>.</summary>
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
    ICurrentUser currentUser,
    IRequestInfo requestInfo,
    IValidator<HoldRequest> holdRules,
    IValidator<CreateBookingRequest> createRules,
    Waitlist.IWaitlistService waitlist,
    Payments.IPaymentsService payments,
    BookingNotices notices,
    Money.ILedger ledger,
    IUnitOfWork unitOfWork) : IBookingsService
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
        var paymentUrl = await PayAsync(booking, plan, me, ct);
        await bookings.SaveAsync(booking, plan.Slots, ct);
        await waitlist.BookedAsync(booking, ct);
        if (booking.WaiverVersionId is { } waiver)
        {
            legal.AddAcceptance(waiver, me, "booking", requestInfo.IpAddress);
        }
        if (booking.Status == BookingStatus.Confirmed)
        {
            await ledger.BookingPaidAsync(booking, ct);
            await unitOfWork.SaveChangesAsync(ct);
            await notices.ConfirmedAsync(booking, plan.Pitch.Name, plan.Pitch.OwnerUserId, ct);
        }
        return BookingMapper.ToResponse(booking, plan.Pitch.Slug, plan.Pitch.Name, plan.Pitch.Address, qr.Create(booking.Code)) with { PaymentUrl = paymentUrl };
    }

    /// <summary>
    /// Online bookings get a payment. The simulated gateway pays at once (booking confirmed); a real gateway returns its page
    /// and the booking waits for the webhook (AwaitingPayment, hours kept for 15 minutes).
    /// </summary>
    private async Task<string?> PayAsync(Booking booking, BookingPlan plan, Guid me, CancellationToken ct)
    {
        if (booking.PayAtVenue || booking.Total <= 0)
        {
            return null;
        }
        var started = await payments.StartAsync(new Payments.PaymentRequest(PaymentPurpose.Booking, booking.Total, me,
            $"Booking {booking.Code} at {plan.Pitch.Name}", $"#/booking/{booking.Code}"), ct);
        booking.Payment = started.Payment;
        if (started.RedirectUrl is not null)
        {
            booking.Status = BookingStatus.AwaitingPayment;
        }
        return started.RedirectUrl;
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
    public static BookingResponse ToResponse(Booking b, string slug, string name, string address, string? qrToken, string? packageCode = null) => new(
        b.Code, slug, name, address, b.Date, b.StartHour, b.Duration, b.PitchAmount, b.ServiceFee, b.Total, b.PayAtVenue,
        b.Status.ToString(), b.PolicyKey, b.RefundAmount, b.CreatedAt, b.CancelledAt, qrToken, packageCode);
}
