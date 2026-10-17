using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Tests.Api;

/// <summary>Holding, booking, my bookings, QR and cancelling (API §3.3) on the demo pitches, with their edge cases.</summary>
[Trait("Category", "Database")]
public sealed class BookingsTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private const string Pitch = "zayed-22";   // open 9 AM – 2 AM, takes cash

    /// <summary>Each test uses its own day so tests never compete for the same hour.</summary>
    private static string Day(int daysAhead) => DateOnly.FromDateTime(DateTime.UtcNow.AddDays(daysAhead)).ToString("yyyy-MM-dd");

    private async Task<ApiClient> CustomerAsync()
    {
        var client = await api.ClientAsync();
        var email = $"booker.{Guid.NewGuid():N}"[..20] + "@test.example";
        var (status, body) = await client.PostAsync("/api/v1/auth/sign-up", new { fullName = "Test Booker", email, phone = "+2015" + Random.Shared.Next(10_000_000, 99_999_999), password = "Football2026", acceptTerms = true });
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        await client.RefreshCsrfAsync();
        return client;
    }

    private static object Booking(string date, int hour, int duration = 1, bool cash = false, bool waiver = true) =>
        new { pitchSlug = Pitch, date, hour, duration, payAtVenue = cash, acceptWaiver = waiver };

    [Fact]
    public async Task A_hold_prices_the_hours_and_blocks_other_people_for_10_minutes()
    {
        var first = await CustomerAsync();
        var second = await CustomerAsync();
        var day = Day(3);

        var hold = await first.PostAsync("/api/v1/bookings/hold", new { pitchSlug = Pitch, date = day, hour = 17, duration = 2 });
        hold.Status.ShouldBe(HttpStatusCode.Created, hold.Body.ToString());
        hold.Body.Data().GetProperty("hours").GetArrayLength().ShouldBe(2);
        hold.Body.Data().GetProperty("total").GetDecimal().ShouldBeGreaterThan(hold.Body.Data().GetProperty("pitchAmount").GetDecimal());

        var other = await second.PostAsync("/api/v1/bookings/hold", new { pitchSlug = Pitch, date = day, hour = 18, duration = 1 });
        other.Status.ShouldBe(HttpStatusCode.Conflict);
        other.Body.Code().ShouldBe("slot_held");
        (await first.PostAsync("/api/v1/bookings/hold", new { pitchSlug = Pitch, date = day, hour = 17, duration = 2 })).Status.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task One_hour_can_be_booked_once_and_a_repeated_request_returns_the_same_booking()
    {
        var first = await CustomerAsync();
        var second = await CustomerAsync();
        var day = Day(4);

        var booked = await first.PostAsync("/api/v1/bookings", Booking(day, 20));
        booked.Status.ShouldBe(HttpStatusCode.Created, booked.Body.ToString());
        var code = booked.Body.Data().GetProperty("code").GetString();
        booked.Body.Data().GetProperty("qrToken").GetString().ShouldNotBeNullOrEmpty();

        (await first.PostAsync("/api/v1/bookings", Booking(day, 20))).Body.Data().GetProperty("code").GetString().ShouldBe(code);
        var taken = await second.PostAsync("/api/v1/bookings", Booking(day, 20));
        taken.Status.ShouldBe(HttpStatusCode.Conflict);
        taken.Body.Code().ShouldBe("slot_taken");

        var grid = (await second.GetAsync($"/api/v1/pitches/{Pitch}/availability?from={day}&days=2")).Body.Data().GetProperty("days")[0].GetProperty("hours");
        grid.EnumerateArray().Single(h => h.GetProperty("hour").GetInt32() == 20).GetProperty("status").GetString().ShouldBe("booked");
    }

    [Fact]
    public async Task An_hour_after_midnight_is_stored_on_the_next_date()
    {
        var client = await CustomerAsync();
        var day = Day(5);
        var booked = (await client.PostAsync("/api/v1/bookings", Booking(day, 25))).Body.Data();

        booked.GetProperty("date").GetString().ShouldBe(Day(6));
        booked.GetProperty("startHour").GetInt32().ShouldBe(1);
        var mine = (await client.GetAsync("/api/v1/bookings/mine")).Body.Data();
        mine.EnumerateArray().ShouldContain(b => b.GetProperty("code").GetString() == booked.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Booking_rules_are_checked()
    {
        var client = await CustomerAsync();
        var day = Day(6);

        var noWaiver = await client.PostAsync("/api/v1/bookings", Booking(day, 12, waiver: false));
        noWaiver.Status.ShouldBe(HttpStatusCode.BadRequest);
        noWaiver.Body.HasFieldError("acceptWaiver").ShouldBeTrue();

        await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Pitches.Pitch>().Where(p => p.Slug == "gezira-fives")
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.AllowCash, false)));
        var cash = await client.PostAsync("/api/v1/bookings", new { pitchSlug = "gezira-fives", date = day, hour = 12, duration = 1, payAtVenue = true, acceptWaiver = true });
        cash.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        cash.Body.Code().ShouldBe("cash_not_allowed");
        (await client.PostAsync("/api/v1/bookings", Booking(day, 13, cash: true))).Status.ShouldBe(HttpStatusCode.Created);

        var early = await client.PostAsync("/api/v1/bookings", Booking(day, 7));
        early.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        early.Body.Code().ShouldBe("outside_hours");

        (await client.PostAsync("/api/v1/bookings", Booking(day, 12, duration: 5))).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await client.PostAsync("/api/v1/bookings", new { pitchSlug = "no-such-pitch", date = day, hour = 12, duration = 1, payAtVenue = false, acceptWaiver = true })).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_booking_is_private_to_its_customer_and_has_a_qr_picture()
    {
        var owner = await CustomerAsync();
        var stranger = await CustomerAsync();
        var code = (await owner.PostAsync("/api/v1/bookings", Booking(Day(7), 14))).Body.Data().GetProperty("code").GetString();

        (await owner.GetAsync($"/api/v1/bookings/{code}")).Body.Data().GetProperty("qrToken").GetString().ShouldNotBeNullOrEmpty();
        (await stranger.GetAsync($"/api/v1/bookings/{code}")).Status.ShouldBe(HttpStatusCode.NotFound);
        var png = await owner.Http.GetAsync($"/api/v1/bookings/{code}/qr");
        png.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await (await api.ClientAsync()).GetAsync("/api/v1/bookings/mine")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cancelling_early_refunds_everything_and_frees_the_hour()
    {
        var client = await CustomerAsync();
        var other = await CustomerAsync();
        var day = Day(8);
        var booked = (await client.PostAsync("/api/v1/bookings", Booking(day, 15))).Body.Data();
        var code = booked.GetProperty("code").GetString();

        var preview = (await client.GetAsync($"/api/v1/bookings/{code}/refund-preview")).Body.Data();
        preview.GetProperty("refundPercent").GetInt32().ShouldBe(100);
        var cancelled = (await client.PostAsync($"/api/v1/bookings/{code}/cancel")).Body.Data();
        cancelled.GetProperty("status").GetString().ShouldBe("CancelledByCustomer");
        cancelled.GetProperty("refundAmount").GetDecimal().ShouldBe(booked.GetProperty("total").GetDecimal());

        (await client.PostAsync($"/api/v1/bookings/{code}/cancel")).Body.Code().ShouldBe("cannot_cancel");
        (await other.PostAsync("/api/v1/bookings", Booking(day, 15))).Status.ShouldBe(HttpStatusCode.Created);
        (await client.GetAsync("/api/v1/bookings/mine?tab=cancelled")).Body.Data().GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task The_ceo_kill_switch_pauses_bookings()
    {
        var client = await CustomerAsync();
        await SwitchAsync(false);
        try
        {
            var paused = await client.PostAsync("/api/v1/bookings", Booking(Day(9), 16));
            paused.Status.ShouldBe(HttpStatusCode.Locked);
        }
        finally
        {
            await SwitchAsync(true);
        }
        (await api.DbAsync(db => db.Set<Booking>().CountAsync(b => b.Date == DateOnly.Parse(Day(9), System.Globalization.CultureInfo.InvariantCulture)))).ShouldBe(0);
    }

    private Task<int> SwitchAsync(bool on) => api.DbAsync(async db =>
    {
        var flag = await db.Set<FeatureFlag>().SingleAsync(f => f.Key == "kill.bookings");
        flag.IsEnabled = on;
        return await db.SaveChangesAsync();
    });
}
