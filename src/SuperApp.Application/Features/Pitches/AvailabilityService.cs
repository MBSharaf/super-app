using FluentValidation;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;

namespace SuperApp.Application.Features.Pitches;

/// <summary>Reads a live pitch's availability data for a date range (holds still running, bookings, closures…).</summary>
public interface IAvailabilityReader
{
    Task<AvailabilityData?> ReadAsync(string slug, DateOnly from, DateOnly to, DateTime nowUtc, CancellationToken ct);
}

/// <summary>The hour grid of a pitch (API §3.1 availability): free, booked, held, closed or past, with the price of each hour.</summary>
public interface IAvailabilityService
{
    Task<AvailabilityDto> GetAsync(string slug, AvailabilityQuery query, CancellationToken ct);
}

public sealed class AvailabilityService(IAvailabilityReader reader, IClock clock, IValidator<AvailabilityQuery> rules) : IAvailabilityService
{
    public async Task<AvailabilityDto> GetAsync(string slug, AvailabilityQuery query, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(query, ct);
        var now = CairoTime.Now(clock);
        var from = query.From ?? DateOnly.FromDateTime(now);
        // One extra calendar day: hours after midnight of the last day are stored on the next date
        var data = await reader.ReadAsync(slug, from, from.AddDays(query.Days), clock.UtcNow, ct)
            ?? throw new NotFoundException("This pitch does not exist or is not open for booking.");
        var days = Enumerable.Range(0, query.Days).Select(i => BuildDay(data, from.AddDays(i), now)).ToList();
        return new AvailabilityDto(slug, days);
    }

    /// <summary>One day of the grid from the opening hours of its weekday.</summary>
    public static DayDto BuildDay(AvailabilityData data, DateOnly date, DateTime nowCairo)
    {
        var weekday = (int)date.DayOfWeek;
        var hours = data.OpeningHours.FirstOrDefault(h => h.DayOfWeek == weekday);
        var holiday = data.Holidays.GetValueOrDefault(date);
        if (hours is null || hours.IsClosed)
        {
            return new DayDto(date, weekday, true, holiday, []);
        }
        var slots = Enumerable.Range(hours.OpenHour, hours.CloseHour - hours.OpenHour).Select(h => BuildSlot(data, date, h, nowCairo)).ToList();
        return new DayDto(date, weekday, false, holiday, slots);
    }

    private static SlotDto BuildSlot(AvailabilityData data, DateOnly day, int hour, DateTime nowCairo)
    {
        var date = hour >= 24 ? day.AddDays(1) : day;
        var calendarHour = hour % 24;
        var basePrice = IsPeak(data, day, hour) ? data.PeakPrice!.Value : data.Price;
        var offer = data.Offers.Where(o => Covers(o, day, hour)).Select(o => o.DiscountPercent).DefaultIfEmpty(0).Max();
        var price = Math.Round(basePrice * (100 - offer) / 100, 2);
        var status = Status(data, date, calendarHour, nowCairo);
        return new SlotDto(hour, date, calendarHour, status, price, basePrice, offer, basePrice != data.Price, data.Waiting.GetValueOrDefault((date, calendarHour)));
    }

    private static string Status(AvailabilityData data, DateOnly date, int hour, DateTime nowCairo)
    {
        if (date.ToDateTime(new TimeOnly(hour, 0)) <= nowCairo) { return "past"; }
        if (data.Closures.Any(c => date >= c.FromDate && date <= c.ToDate && (c.FromHour is null || (hour >= c.FromHour && hour < c.ToHour)))) { return "closed"; }
        if (data.Booked.Contains((date, hour))) { return "booked"; }
        return data.Held.Contains((date, hour)) ? "held" : "free";
    }

    /// <summary>Peak = from the peak hour on, after midnight, and all day on Friday and Saturday (Egyptian weekend).</summary>
    private static bool IsPeak(AvailabilityData data, DateOnly day, int hour) =>
        data.PeakPrice is not null && data.PeakFromHour is not null
        && (hour >= data.PeakFromHour || day.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday);

    private static bool Covers(OfferData o, DateOnly day, int hour) =>
        (o.DaysMask & (1 << (int)day.DayOfWeek)) != 0 && hour % 24 >= o.FromHour && hour % 24 < o.ToHour && day >= o.StartsOn && day <= o.EndsOn;
}

/// <summary>Booking hours are Cairo local time.</summary>
public static class CairoTime
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    public static DateTime Now(IClock clock) => TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, Zone);
}

public sealed class AvailabilityQueryValidator : AbstractValidator<AvailabilityQuery>
{
    public AvailabilityQueryValidator()
    {
        RuleFor(x => x.Days).InclusiveBetween(1, 14).WithMessage("Show 1 to 14 days.");
        RuleFor(x => x.From).Must(d => d is null || (d >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)) && d <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90))))
            .WithMessage("Choose a date from today up to 90 days ahead.");
    }
}
