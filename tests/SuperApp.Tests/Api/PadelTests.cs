using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Tests.Api;

/// <summary>
/// Padel (owner, 2026-11-21): a court is a pitch with Sport = Padel, booked from any half hour for the 60, 90 or 120 minutes
/// its owner offers, on the platform's rules — deposit or full payment, commission, QR check-in — with rackets and balls
/// rented at the club.
/// </summary>
[Trait("Category", "Database")]
public sealed class PadelTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
    private readonly PartnerKit kit = new(api);

    private static Dictionary<string, object?> Court(long venueId, int[] durations, decimal? racket = 50, decimal? balls = 80, string? rowVersion = null)
    {
        var pitch = PartnerKit.Pitch(venueId, "Court 1", 600, rowVersion);   // 750 an hour from 18:00
        var fields = pitch.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(pitch));
        fields["sport"] = "Padel";
        fields["format"] = 2;
        fields["isPanoramic"] = true;
        fields["isIndoor"] = true;
        fields["durations"] = durations;
        fields["racketPrice"] = racket;
        fields["ballsPrice"] = balls;
        return fields;
    }

    private async Task<(ApiClient Partner, long Id, string Slug)> LiveCourtAsync(params int[] durations)
    {
        var (partner, _, venueId, _) = await kit.PartnerWithPitchAsync();
        var made = await partner.PostAsync("/api/v1/partner/pitches", Court(venueId, durations.Length == 0 ? [60, 90, 120] : durations));
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var id = made.Body.Data().GetProperty("id").GetInt64();
        await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Pitches.Pitch>().Where(p => p.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, SuperApp.Domain.Enums.PitchStatus.Live)));
        return (partner, id, made.Body.Data().GetProperty("slug").GetString()!);
    }

    private static object Booking(string slug, string date, int hour, int minute, int minutes, int rackets = 0, int balls = 0) =>
        new { pitchSlug = slug, date, hour, minute, minutes, duration = 1, rackets, balls, payAtVenue = false, acceptWaiver = true };

    [Fact]
    public async Task An_owner_adds_a_court_with_its_lengths_and_rentals_and_players_find_it_under_padel()
    {
        var (partner, id, slug) = await LiveCourtAsync(60, 90);
        var edit = (await partner.GetAsync($"/api/v1/partner/pitches/{id}")).Body.Data();
        edit.GetProperty("sport").GetString().ShouldBe("Padel");
        edit.GetProperty("isPanoramic").GetBoolean().ShouldBeTrue();
        edit.GetProperty("durations").EnumerateArray().Select(d => d.GetInt32()).ShouldBe([60, 90]);
        edit.GetProperty("racketPrice").GetDecimal().ShouldBe(50);

        var list = await partner.GetAsync("/api/v1/pitches?sport=Padel&panoramic=true&q=Court%201&pageSize=50");
        list.Status.ShouldBe(HttpStatusCode.OK, list.Body.ToString());
        var padel = list.Body.Data().EnumerateArray().ToList();
        padel.ShouldContain(c => c.GetProperty("slug").GetString() == slug);
        padel.ShouldAllBe(c => c.GetProperty("sport").GetString() == "Padel");
        (await partner.GetAsync("/api/v1/pitches?q=Court%201&pageSize=50")).Body.Data().EnumerateArray().ShouldNotContain(c => c.GetProperty("slug").GetString() == slug);

        var page = (await partner.GetAsync($"/api/v1/pitches/{slug}")).Body.Data();
        page.GetProperty("durations").EnumerateArray().Select(d => d.GetInt32()).ShouldBe([60, 90]);
        var day = (await partner.GetAsync($"/api/v1/pitches/{slug}/availability?from={PartnerKit.Day(3)}&days=1")).Body.Data().GetProperty("days")[0];
        var halves = day.GetProperty("halves").EnumerateArray().ToList();
        halves.Count.ShouldBe(34);   // 09:00 to 02:00, every half hour
        halves.First(h => h.GetProperty("time").GetString() == "19:30").GetProperty("price").GetDecimal().ShouldBe(375);   // peak 750 an hour

        // A court keeps its sport, needs a length, and is for doubles
        var version = edit.GetProperty("rowVersion").GetString();
        var football = Court(edit.GetProperty("venueId").GetInt64(), [60], rowVersion: version);
        football["sport"] = "Football";
        (await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}", football)).Status.ShouldBe(HttpStatusCode.BadRequest);
        var none = Court(edit.GetProperty("venueId").GetInt64(), []);
        (await partner.PostAsync("/api/v1/partner/pitches", none)).Body.HasFieldError("durations").ShouldBeTrue();
        var five = Court(edit.GetProperty("venueId").GetInt64(), [60]);
        five["format"] = 5;
        (await partner.PostAsync("/api/v1/partner/pitches", five)).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_player_books_90_minutes_from_the_half_hour_and_overlapping_times_are_taken()
    {
        var (_, _, slug) = await LiveCourtAsync(60, 90);
        var (player, _) = await kit.PersonAsync(partner: false);
        var hold = (await player.PostAsync("/api/v1/bookings/hold", new { pitchSlug = slug, date = PartnerKit.Day(4), hour = 19, minute = 30, minutes = 90 })).Body.Data();
        hold.GetProperty("hours").GetArrayLength().ShouldBe(3);

        var made = await player.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(4), 19, 30, 90, rackets: 2, balls: 1));
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var b = made.Body.Data();
        b.GetProperty("sport").GetString().ShouldBe("Padel");
        (b.GetProperty("startHour").GetInt32(), b.GetProperty("startMinute").GetInt32(), b.GetProperty("minutes").GetInt32()).ShouldBe((19, 30, 90));
        b.GetProperty("pitchAmount").GetDecimal().ShouldBe(375 * 3);   // three peak half hours
        b.GetProperty("extrasAmount").GetDecimal().ShouldBe(2 * 50 + 80);
        (b.GetProperty("deposit").GetDecimal() + b.GetProperty("balanceDue").GetDecimal()).ShouldBe(b.GetProperty("total").GetDecimal());   // extras stay out of the total

        // 20:30 is inside the booking; 21:00 is free again
        var (other, _) = await kit.PersonAsync(partner: false);
        var clash = await other.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(4), 20, 30, 60));
        clash.Status.ShouldBe(HttpStatusCode.Conflict, clash.Body.ToString());
        (await other.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(4), 21, 0, 60))).Status.ShouldBe(HttpStatusCode.Created);

        // Only the lengths the court offers, starting on the hour or the half hour
        (await other.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(5), 18, 0, 120))).Body.Code().ShouldBe("duration_not_offered");
        (await other.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(5), 18, 15, 60))).Body.HasFieldError("minute").ShouldBeTrue();
        (await other.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(5), 18, 0, 60, rackets: 9))).Body.HasFieldError("rackets").ShouldBeTrue();
    }

    [Fact]
    public async Task The_club_scans_the_qr_and_collects_the_rest_with_the_rentals()
    {
        var (partner, _, slug) = await LiveCourtAsync();
        var (player, _) = await kit.PersonAsync(partner: false);
        var b = (await player.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(6), 18, 30, 60, rackets: 1))).Body.Data();
        var code = b.GetProperty("code").GetString();
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Cairo);
        var start = now.AddMinutes(-10);
        await api.DbAsync(db => db.Set<Booking>().Where(x => x.Code == code).ExecuteUpdateAsync(u => u
            .SetProperty(x => x.Date, DateOnly.FromDateTime(start)).SetProperty(x => x.StartHour, (byte)start.Hour).SetProperty(x => x.StartMinute, (byte)(start.Minute >= 30 ? 30 : 0))));
        var kickOff = new DateTime(start.Year, start.Month, start.Day, start.Hour, start.Minute >= 30 ? 30 : 0, 0);   // at most 40 minutes ago, so the hour is still on

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/check-in/scan") { Content = JsonContent.Create(new { token = b.GetProperty("qrToken").GetString(), device = "Club desk" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await partner.Http.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var scan = (await response.Content.ReadFromJsonAsync<JsonElement>()).Data();
        scan.GetProperty("result").GetString().ShouldBe("accepted");
        scan.GetProperty("minutes").GetInt32().ShouldBe(60);
        scan.GetProperty("endsAt").GetString().ShouldBe(kickOff.AddMinutes(60).ToString("HH:mm"));
        scan.GetProperty("extrasAmount").GetDecimal().ShouldBe(50);
        scan.GetProperty("message").GetString()!.ShouldContain("Hand over 1 racket and collect 50 EGP");
    }

    [Fact]
    public async Task Weekly_packages_and_waitlists_stay_football_only_for_now()
    {
        var (_, _, slug) = await LiveCourtAsync();
        var (player, _) = await kit.PersonAsync(partner: false);
        var join = await player.PostAsync("/api/v1/waitlist", new { pitchSlug = slug, date = PartnerKit.Day(3), hour = 20 });
        join.Status.ShouldBe(HttpStatusCode.UnprocessableEntity, join.Body.ToString());
        join.Body.Code().ShouldBe("football_only");
    }

    [Fact]
    public async Task An_open_match_is_four_players_on_the_padel_scale_and_everyone_hears_when_it_is_full()
    {
        var (_, _, slug) = await LiveCourtAsync(90);
        var (host, hostId) = await kit.PersonAsync(partner: false);
        var booking = (await host.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(7), 19, 30, 90))).Body.Data();
        var code = booking.GetProperty("code").GetString();
        object Match(int needed, string level) => new { bookingCode = code, playersNeeded = needed, level, positions = (string?)null, title = (string?)null, sharePrice = 1m, autoAccept = true };

        (await host.PostAsync("/api/v1/games", Match(4, "Level4"))).Body.Code().ShouldBe("open_match_players");
        (await host.PostAsync("/api/v1/games", Match(1, "Casual"))).Body.Code().ShouldBe("padel_level");
        var posted = await host.PostAsync("/api/v1/games", Match(1, "Level4"));
        posted.Status.ShouldBe(HttpStatusCode.Created, posted.Body.ToString());
        var game = posted.Body.Data().GetProperty("game");
        game.GetProperty("sport").GetString().ShouldBe("Padel");
        (game.GetProperty("startMinute").GetInt32(), game.GetProperty("minutes").GetInt32()).ShouldBe((30, 90));
        game.GetProperty("level").GetString().ShouldBe("Level4");
        game.GetProperty("sharePrice").GetDecimal().ShouldBe(Math.Ceiling(booking.GetProperty("total").GetDecimal() / 4));   // the court split by 4, whatever the app sent

        var (player, playerId) = await kit.PersonAsync(partner: false);
        (await player.PostAsync($"/api/v1/games/{game.GetProperty("code").GetString()}/requests", new { position = "Drive side (right)" })).Body.HasFieldError("position").ShouldBeTrue();
        var join = await player.PostAsync($"/api/v1/games/{game.GetProperty("code").GetString()}/requests", new { position = "Drive" });
        join.Status.ShouldBe(HttpStatusCode.Created, join.Body.ToString());
        var joined = join.Body.Data();
        joined.GetProperty("game").GetProperty("status").GetString().ShouldBe("Full");
        foreach (var who in new[] { hostId, playerId })
        {
            (await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Notifications.Notification>().AnyAsync(n => n.UserId == who && n.Title.StartsWith("Your Open Match is full")))).ShouldBeTrue();
        }
    }
}
