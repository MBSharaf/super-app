using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Bookings;

/// <summary>A checked request: the pitch, each hour with its price, and the money.</summary>
/// <remarks>
/// Padel courts (<c>Minutes</c> &gt; 0): each line is a half hour, <c>Hour</c> its index on the day's grid and
/// <c>CalendarHour</c> its stored index (hour × 2, + 1 for :30) — what the slots of a court hold.
/// </remarks>
public sealed record BookingPlan(BookablePitch Pitch, IReadOnlyList<PriceLine> Lines, decimal PitchAmount, decimal ServiceFee, decimal Total, int Minutes = 0)
{
    public IReadOnlyList<(DateOnly Date, int Hour)> Slots => Lines.Select(l => (l.Date, l.CalendarHour)).ToList();

    public bool IsCourt => Minutes > 0;

    /// <summary>Where the booking starts: the calendar hour and minute (a court's first half hour, or a pitch's first hour).</summary>
    public (byte Hour, byte Minute) Start => IsCourt
        ? ((byte)(Lines[0].CalendarHour / 2), (byte)(Lines[0].CalendarHour % 2 * 30))
        : ((byte)Lines[0].CalendarHour, (byte)0);

    /// <summary>Whole hours the booking touches (a 90-minute court booking counts 2).</summary>
    public byte Hours(int requestedHours) => IsCourt ? (byte)((Minutes + 59) / 60) : (byte)requestedHours;
}

/// <summary>
/// Checks that the hours asked for exist on the pitch's grid, are in the future and free (or held), and prices
/// them with the same rules as the grid. Shared by hold and confirm.
/// </summary>
public sealed class BookingPlanner(IBookingRepository bookings, IAvailabilityReader availability, ISettingsReader settings, IClock clock)
{
    /// <param name="allowStarted">Walk-ins: the hour that is being played now can still be booked at the venue
    /// (the database's unique index still stops a double booking).</param>
    /// <param name="minute">Padel courts: 0 or 30.</param>
    /// <param name="minutes">Padel courts: 60, 90 or 120 (one the court offers); a walk-in without it books whole hours.</param>
    /// <param name="courts">
    /// True where a padel court can be booked (player bookings, walk-ins). Weekly packages, waitlists, tournaments and
    /// free-hour posts are football only for now, so they leave it false and a court is refused.
    /// </param>
    public async Task<BookingPlan> PlanAsync(string slug, DateOnly date, int hour, int duration, CancellationToken ct, bool allowStarted = false,
        int minute = 0, int? minutes = null, bool courts = false)
    {
        var pitch = await bookings.PitchAsync(slug, ct) ?? throw new NotFoundException("This pitch does not exist or is not open for booking.");
        var data = await availability.ReadAsync(slug, date, date.AddDays(1), clock.UtcNow, ct) ?? throw new NotFoundException();
        var day = AvailabilityService.BuildDay(data, date, CairoTime.Now(clock));
        if (pitch.Sport == Sport.Padel)
        {
            if (!courts)
            {
                throw new BusinessRuleException("This is for football pitches only for now.", "football_only");
            }
            return await CourtAsync(pitch, day, hour, minute, minutes ?? duration * 60, allowStarted, ct);
        }
        if (minute != 0)
        {
            throw new BadRequestException("Football pitches are booked by the hour.", "minute");
        }
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
        var lines = hours.Select(h => new PriceLine(h.Hour, h.Date, h.CalendarHour, h.Price)).ToList();
        return await PricedAsync(pitch, lines, 0, ct);
    }

    /// <summary>A padel court: 2, 3 or 4 free half hours in a row from the start asked for, in a length the court offers.</summary>
    private async Task<BookingPlan> CourtAsync(BookablePitch pitch, DayDto day, int hour, int minute, int minutes, bool allowStarted, CancellationToken ct)
    {
        if (!pitch.Durations.Allows(minutes))
        {
            throw new BusinessRuleException($"This court is booked for {string.Join(", ", pitch.Durations.ToMinutes())} minutes.", "duration_not_offered");
        }
        var start = hour * 2 + minute / 30;
        var halves = (day.Halves ?? []).Where(h => h.Hour * 2 + h.Minute / 30 >= start && h.Hour * 2 + h.Minute / 30 < start + minutes / 30).ToList();
        if (halves.Count != minutes / 30)
        {
            throw new BusinessRuleException("The court is not open at these times.", "outside_hours");
        }
        var now = CairoTime.Now(clock);
        foreach (var h in halves.Where(h => !(allowStarted && h.Status == "past" && h.Date.ToDateTime(new TimeOnly(h.Index / 2, h.Index % 2 * 30)) > now.AddMinutes(-30))))
        {
            CheckStatus(h.Status);
        }
        var lines = halves.Select(h => new PriceLine(h.Hour * 2 + h.Minute / 30, h.Date, h.Index, h.Price)).ToList();
        return await PricedAsync(pitch, lines, minutes, ct);
    }

    private async Task<BookingPlan> PricedAsync(BookablePitch pitch, List<PriceLine> lines, int minutes, CancellationToken ct)
    {
        var amount = lines.Sum(l => l.Price);
        var feeRate = await settings.DecimalAsync("fees.service", 0.03m, ct);
        var fee = Math.Round(amount * feeRate, 0, MidpointRounding.AwayFromZero);
        return new BookingPlan(pitch, lines, amount, fee, amount + fee, minutes);
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
