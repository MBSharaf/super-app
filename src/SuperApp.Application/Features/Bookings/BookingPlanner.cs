using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Pitches;

namespace SuperApp.Application.Features.Bookings;

/// <summary>A checked request: the pitch, each hour with its price, and the money.</summary>
public sealed record BookingPlan(BookablePitch Pitch, IReadOnlyList<PriceLine> Lines, decimal PitchAmount, decimal ServiceFee, decimal Total)
{
    public IReadOnlyList<(DateOnly Date, int Hour)> Slots => Lines.Select(l => (l.Date, l.CalendarHour)).ToList();
}

/// <summary>
/// Checks that the hours asked for exist on the pitch's grid, are in the future and free (or held), and prices
/// them with the same rules as the grid. Shared by hold and confirm.
/// </summary>
public sealed class BookingPlanner(IBookingRepository bookings, IAvailabilityReader availability, ISettingsReader settings, IClock clock)
{
    /// <param name="allowStarted">Walk-ins: the hour that is being played now can still be booked at the venue
    /// (the database's unique index still stops a double booking).</param>
    public async Task<BookingPlan> PlanAsync(string slug, DateOnly date, int hour, int duration, CancellationToken ct, bool allowStarted = false)
    {
        var pitch = await bookings.PitchAsync(slug, ct) ?? throw new NotFoundException("This pitch does not exist or is not open for booking.");
        var data = await availability.ReadAsync(slug, date, date.AddDays(1), clock.UtcNow, ct) ?? throw new NotFoundException();
        var day = AvailabilityService.BuildDay(data, date, CairoTime.Now(clock));
        var hours = day.Hours.Where(h => h.Hour >= hour && h.Hour < hour + duration).OrderBy(h => h.Hour).ToList();
        if (hours.Count != duration)
        {
            throw new BusinessRuleException("The pitch is not open at these hours.", "outside_hours");
        }
        var now = CairoTime.Now(clock);
        foreach (var h in hours.Where(h => !(allowStarted && h.Status == "past" && h.Date.ToDateTime(new TimeOnly(h.CalendarHour, 0)) > now.AddHours(-1))))
        {
            CheckStatus(h.Status);
        }
        var amount = hours.Sum(h => h.Price);
        var feeRate = await settings.DecimalAsync("fees.service", 0.03m, ct);
        var fee = Math.Round(amount * feeRate, 0, MidpointRounding.AwayFromZero);
        var lines = hours.Select(h => new PriceLine(h.Hour, h.Date, h.CalendarHour, h.Price)).ToList();
        return new BookingPlan(pitch, lines, amount, fee, amount + fee);
    }

    private static void CheckStatus(string status)
    {
        switch (status)
        {
            case "past": throw new BusinessRuleException("This hour has already started.", "hour_past");
            case "closed": throw new BusinessRuleException("The pitch is closed at this hour.", "hour_closed");
            case "booked": throw new ConflictException("Someone just booked this hour. Please choose another one.", "slot_taken");
        }
    }
}
