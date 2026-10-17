using System.Net;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>Public pitches (API §3.1) on the demo data: search, filters, sorting, the pitch page and its edge cases.</summary>
[Trait("Category", "Database")]
public sealed class PitchesTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    [Fact]
    public async Task Guests_see_every_live_pitch_paged_and_sorted_by_rating()
    {
        var guest = await api.ClientAsync();
        var (status, body) = await guest.GetAsync("/api/v1/pitches?pageSize=5");

        status.ShouldBe(HttpStatusCode.OK);
        body.GetProperty("meta").GetProperty("total").GetInt32().ShouldBe(12);
        body.GetProperty("meta").GetProperty("totalPages").GetInt32().ShouldBe(3);
        var ratings = body.Data().EnumerateArray().Select(p => p.GetProperty("rating").GetDecimal()).ToList();
        ratings.ShouldBe(ratings.OrderByDescending(r => r).ToList());
        body.Data()[0].GetProperty("photo").GetProperty("key").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Filters_narrow_the_list()
    {
        var guest = await api.ClientAsync();
        var fives = (await guest.GetAsync("/api/v1/pitches?format=5&pageSize=50")).Body.Data().EnumerateArray().ToList();
        fives.Count.ShouldBe(6);
        fives.ShouldAllBe(p => p.GetProperty("format").GetInt32() == 5);

        var cheap = (await guest.GetAsync("/api/v1/pitches?maxPrice=600&sort=price&pageSize=50")).Body.Data().EnumerateArray().Select(p => p.GetProperty("pricePerHour").GetDecimal()).ToList();
        cheap.ShouldAllBe(p => p <= 600);
        cheap.ShouldBe(cheap.Order().ToList());

        var lit = (await guest.GetAsync("/api/v1/pitches?facilities=lights,camera&pageSize=50")).Body.Data().EnumerateArray().ToList();
        lit.ShouldNotBeEmpty();
        lit.ShouldAllBe(p => p.GetProperty("facilities").EnumerateArray().Any(f => f.GetString() == "camera"));

        (await guest.GetAsync("/api/v1/pitches?q=Zamalek")).Body.Data()[0].GetProperty("areaEn").GetString().ShouldBe("Zamalek");
    }

    [Fact]
    public async Task Sorting_by_distance_needs_a_location_and_shows_kilometres()
    {
        var guest = await api.ClientAsync();
        var missing = await guest.GetAsync("/api/v1/pitches?sort=distance");
        missing.Status.ShouldBe(HttpStatusCode.BadRequest);

        var near = (await guest.GetAsync("/api/v1/pitches?sort=distance&lat=30.0566&lng=31.2215&pageSize=3")).Body.Data();
        near[0].GetProperty("slug").GetString().ShouldBe("gezira-fives");
        near[0].GetProperty("distanceKm").GetDouble().ShouldBeLessThan(1);
        near[1].GetProperty("distanceKm").GetDouble().ShouldBeGreaterThanOrEqualTo(near[0].GetProperty("distanceKm").GetDouble());
    }

    [Theory]
    [InlineData("format=6")]
    [InlineData("minPrice=500&maxPrice=100")]
    [InlineData("sort=cheapest")]
    [InlineData("pageSize=500")]
    [InlineData("lat=10&lng=31")]
    public async Task Bad_filters_are_refused(string query)
    {
        var (status, _) = await (await api.ClientAsync()).GetAsync($"/api/v1/pitches?{query}");
        status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_pitch_page_has_photos_policy_hours_and_owner()
    {
        var page = (await (await api.ClientAsync()).GetAsync("/api/v1/pitches/zayed-22")).Body.Data();

        page.GetProperty("card").GetProperty("name").GetString().ShouldBe("Turf 22 Arena");
        page.GetProperty("photos").GetArrayLength().ShouldBe(4);
        page.GetProperty("openingHours").GetArrayLength().ShouldBe(7);
        page.GetProperty("cancellationPolicy").GetProperty("key").GetString().ShouldNotBeNullOrEmpty();
        page.GetProperty("partnerName").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task An_unknown_pitch_is_404()
    {
        var (status, body) = await (await api.ClientAsync()).GetAsync("/api/v1/pitches/no-such-pitch");
        status.ShouldBe(HttpStatusCode.NotFound);
        body.Code().ShouldBe("not_found");
    }

    [Fact]
    public async Task The_hour_grid_shows_overnight_hours_and_prices()
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)).ToString("yyyy-MM-dd");
        var (status, body) = await (await api.ClientAsync()).GetAsync($"/api/v1/pitches/zayed-22/availability?from={from}&days=3");

        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        var days = body.Data().GetProperty("days");
        days.GetArrayLength().ShouldBe(3);
        var hours = days[0].GetProperty("hours");
        hours.GetArrayLength().ShouldBe(17);   // Turf 22 Arena: 9 AM to 2 AM
        hours[16].GetProperty("hour").GetInt32().ShouldBe(25);
        hours[16].GetProperty("calendarHour").GetInt32().ShouldBe(1);
        hours.EnumerateArray().ShouldAllBe(h => h.GetProperty("status").GetString() == "free");
    }

    [Theory]
    [InlineData("days=0")]
    [InlineData("days=15")]
    [InlineData("from=2020-01-01")]
    public async Task The_hour_grid_refuses_bad_ranges(string query)
    {
        var (status, _) = await (await api.ClientAsync()).GetAsync($"/api/v1/pitches/zayed-22/availability?{query}");
        status.ShouldBe(HttpStatusCode.BadRequest);
    }
}
