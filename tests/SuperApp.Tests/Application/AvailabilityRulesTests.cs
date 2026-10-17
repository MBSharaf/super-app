using Shouldly;
using SuperApp.Application.Features.Pitches;

namespace SuperApp.Tests.Application;

/// <summary>The hour grid rules, without a database: overnight hours, peak prices, offers, closures, bookings, past hours.</summary>
public sealed class AvailabilityRulesTests
{
    // Thursday 22 Oct 2026; the grid is built "now" = Wednesday noon, so every hour is in the future
    private static readonly DateOnly Thursday = new(2026, 10, 22);
    private static readonly DateTime Now = new(2026, 10, 21, 12, 0, 0);

    private static AvailabilityData Data(
        IReadOnlySet<(DateOnly, int)>? booked = null, IReadOnlyList<ClosureData>? closures = null, IReadOnlyList<OfferData>? offers = null) => new(
        1, 600, 800, 18,
        Enumerable.Range(0, 7).Select(d => new OpeningHourDto(d, 9, 26, false)).ToList(),
        booked ?? new HashSet<(DateOnly, int)>(), new HashSet<(DateOnly, int)>(),
        closures ?? [], offers ?? [],
        new Dictionary<DateOnly, HolidayBadgeDto>(), new Dictionary<(DateOnly, int), int>());

    [Fact]
    public void Hours_after_midnight_belong_to_the_night_but_are_stored_on_the_next_date()
    {
        var day = AvailabilityService.BuildDay(Data(), Thursday, Now);

        day.Hours.Count.ShouldBe(17);   // 9:00 … 25:00 (1 AM)
        var late = day.Hours[^1];
        late.Hour.ShouldBe(25);
        late.Date.ShouldBe(Thursday.AddDays(1));
        late.CalendarHour.ShouldBe(1);
    }

    [Fact]
    public void Peak_price_from_the_peak_hour_and_all_weekend()
    {
        var thursday = AvailabilityService.BuildDay(Data(), Thursday, Now);
        thursday.Hours.Single(h => h.Hour == 17).Price.ShouldBe(600);
        thursday.Hours.Single(h => h.Hour == 18).Price.ShouldBe(800);
        thursday.Hours.Single(h => h.Hour == 25).IsPeak.ShouldBeTrue();

        var friday = AvailabilityService.BuildDay(Data(), Thursday.AddDays(1), Now);
        friday.Hours.ShouldAllBe(h => h.Price == 800);
    }

    [Fact]
    public void An_offer_lowers_the_price_of_its_hours_only()
    {
        var offer = new OfferData(1 << (int)DayOfWeek.Thursday, 10, 14, 20, Thursday, Thursday);
        var day = AvailabilityService.BuildDay(Data(offers: [offer]), Thursday, Now);

        day.Hours.Single(h => h.Hour == 11).Price.ShouldBe(480);
        day.Hours.Single(h => h.Hour == 11).OfferPercent.ShouldBe(20);
        day.Hours.Single(h => h.Hour == 15).Price.ShouldBe(600);
    }

    [Fact]
    public void Booked_closed_and_past_hours_are_marked()
    {
        var booked = new HashSet<(DateOnly, int)> { (Thursday.AddDays(1), 0) };   // midnight hour, stored on Friday
        var closure = new ClosureData(Thursday, Thursday, 10, 12);
        var day = AvailabilityService.BuildDay(Data(booked, [closure]), Thursday, Thursday.ToDateTime(new TimeOnly(15, 30)));

        day.Hours.Single(h => h.Hour == 24).Status.ShouldBe("booked");
        day.Hours.Single(h => h.Hour == 10).Status.ShouldBe("past");
        day.Hours.Single(h => h.Hour == 20).Status.ShouldBe("free");

        var tomorrow = AvailabilityService.BuildDay(Data(booked, [new ClosureData(Thursday.AddDays(1), Thursday.AddDays(1), null, null)]), Thursday.AddDays(1), Now);
        tomorrow.Hours.Where(h => h.Hour < 24).ShouldAllBe(h => h.Status == "closed");
    }
}
