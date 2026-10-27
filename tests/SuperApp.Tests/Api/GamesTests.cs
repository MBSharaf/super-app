using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Tests.Api;

/// <summary>Find players (API §3.8): post a booking as a game, join, decide, waitlist, withdraw and rate.</summary>
[Trait("Category", "Database")]
public sealed class GamesTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private async Task<(ApiClient Host, string Booking)> HostAsync()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (host, _) = await kit.PersonAsync(partner: false);
        var code = (await host.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(3), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true }))
            .Body.Data().GetProperty("code").GetString()!;
        return (host, code);
    }

    private static object Game(string booking, int needed = 1, bool auto = false) =>
        new { bookingCode = booking, playersNeeded = needed, level = "Casual", positions = "GK", title = "Friday five", sharePrice = 70m, autoAccept = auto, notes = "Bring a dark shirt" };

    [Fact]
    public async Task A_game_fills_up_waitlists_the_rest_and_frees_a_place_when_someone_leaves()
    {
        var (host, booking) = await HostAsync();
        var (stranger, _) = await kit.PersonAsync(partner: false);
        (await stranger.PostAsync("/api/v1/games", Game(booking))).Body.Code().ShouldBe("booking_not_mine");
        var posted = await host.PostAsync("/api/v1/games", Game(booking));
        posted.Status.ShouldBe(HttpStatusCode.Created, posted.Body.ToString());
        var code = posted.Body.Data().GetProperty("game").GetProperty("code").GetString()!;
        (await host.PostAsync("/api/v1/games", Game(booking))).Body.Code().ShouldBe("game_exists");
        (await stranger.GetAsync("/api/v1/games")).Body.Data().EnumerateArray().ShouldContain(g => g.GetProperty("code").GetString() == code);

        var (first, _) = await kit.PersonAsync(partner: false);
        var (second, _) = await kit.PersonAsync(partner: false);
        (await first.PostAsync($"/api/v1/games/{code}/requests", new { position = "GK", message = "Keeper here" })).Body.Data().GetProperty("myStatus").GetString().ShouldBe("Waiting");
        (await first.PostAsync($"/api/v1/games/{code}/requests", new { })).Body.Code().ShouldBe("already_asked");
        var requests = (await host.GetAsync($"/api/v1/games/{code}")).Body.Data().GetProperty("requests");
        var firstId = requests[0].GetProperty("id").GetInt64();
        (await host.PostAsync($"/api/v1/games/{code}/requests/{firstId}/accept")).Body.Data().GetProperty("game").GetProperty("status").GetString().ShouldBe("Full");

        (await second.PostAsync($"/api/v1/games/{code}/requests", new { })).Body.Data().GetProperty("myStatus").GetString().ShouldBe("Waitlisted");
        (await first.SendAsync(HttpMethod.Delete, $"/api/v1/games/{code}/requests/mine")).Status.ShouldBe(HttpStatusCode.OK);
        // The 70 EGP share paid when asking comes back to the wallet when leaving
        (await first.GetAsync("/api/v1/wallet")).Body.Data().EnumerateArray().Single(w => w.GetProperty("type").GetString() == "Customer").GetProperty("available").GetDecimal().ShouldBe(70);
        (await first.GetAsync("/api/v1/receipts")).Body.Data().GetArrayLength().ShouldBe(2);
        var after = (await second.GetAsync($"/api/v1/games/{code}")).Body.Data();
        after.GetProperty("myStatus").GetString().ShouldBe("Waiting");
        after.GetProperty("game").GetProperty("status").GetString().ShouldBe("Open");
    }

    [Fact]
    public async Task Auto_accept_and_ratings_after_kick_off_build_the_player_record()
    {
        var (host, booking) = await HostAsync();
        var code = (await host.PostAsync("/api/v1/games", Game(booking, needed: 2, auto: true))).Body.Data().GetProperty("game").GetProperty("code").GetString()!;
        var (player, playerId) = await kit.PersonAsync(partner: false);
        (await player.PostAsync($"/api/v1/games/{code}/requests", new { })).Body.Data().GetProperty("myStatus").GetString().ShouldBe("Accepted");

        (await host.PostAsync($"/api/v1/games/{code}/ratings", new { ratings = new[] { new { playerId, stars = 5, showedUp = true } } })).Body.Code().ShouldBe("not_started");
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == booking).ExecuteUpdateAsync(u => u.SetProperty(b => b.Date, yesterday)));
        (await host.PostAsync($"/api/v1/games/{code}/ratings", new { ratings = new[] { new { playerId, stars = 5, showedUp = true } } })).Status.ShouldBe(HttpStatusCode.OK);

        var record = (await player.GetAsync($"/api/v1/players/{playerId}/record")).Body.Data();
        record.GetProperty("games").GetInt32().ShouldBe(1);
        record.GetProperty("showedUp").GetInt32().ShouldBe(1);
        record.GetProperty("averageStars").GetDecimal().ShouldBe(5);
    }
    [Fact]
    public async Task My_games_list_auto_accept_and_cancelling_refunds_every_player()
    {
        var (host, booking) = await HostAsync();
        var code = (await host.PostAsync("/api/v1/games", Game(booking, needed: 3))).Body.Data().GetProperty("game").GetProperty("code").GetString()!;
        var (player, _) = await kit.PersonAsync(partner: false);
        (await player.PostAsync($"/api/v1/games/{code}/requests", new { position = "GK" })).Body.Data().GetProperty("myStatus").GetString().ShouldBe("Waiting");

        var detail = (await host.GetAsync($"/api/v1/games/{code}")).Body.Data();
        (detail.GetProperty("autoAccept").GetBoolean(), detail.GetProperty("bookingCode").GetString()).ShouldBe((false, booking));
        detail.GetProperty("game").GetProperty("notes").GetString().ShouldBe("Bring a dark shirt");
        detail.GetProperty("requests")[0].GetProperty("games").GetInt32().ShouldBe(0);
        (await player.GetAsync($"/api/v1/games/{code}")).Body.Data().GetProperty("bookingCode").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        var edit = new { playersNeeded = 3, level = "Casual", positions = "GK", title = "Friday five", requestsClosed = false, autoAccept = true };
        (await host.SendAsync(HttpMethod.Put, $"/api/v1/games/{code}", edit)).Body.Data().GetProperty("autoAccept").GetBoolean().ShouldBeTrue();

        (await host.GetAsync("/api/v1/games/mine")).Body.Data().GetProperty("hosting").EnumerateArray().ShouldContain(g => g.GetProperty("code").GetString() == code);
        var joined = (await player.GetAsync("/api/v1/games/mine")).Body.Data().GetProperty("joined").EnumerateArray().Single(g => g.GetProperty("game").GetProperty("code").GetString() == code);
        joined.GetProperty("myStatus").GetString().ShouldBe("Waiting");

        (await player.PostAsync($"/api/v1/games/{code}/cancel")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await host.PostAsync($"/api/v1/games/{code}/cancel")).Body.Data().GetProperty("game").GetProperty("status").GetString().ShouldBe("Cancelled");
        (await host.PostAsync($"/api/v1/games/{code}/cancel")).Body.Code().ShouldBe("closed");
        (await player.GetAsync("/api/v1/wallet")).Body.Data().EnumerateArray().Single(w => w.GetProperty("type").GetString() == "Customer").GetProperty("available").GetDecimal().ShouldBe(70);
        (await player.GetAsync("/api/v1/games/mine")).Body.Data().GetProperty("joined").GetArrayLength().ShouldBe(0);
        (await host.GetAsync("/api/v1/games/mine")).Body.Data().GetProperty("hosting").GetArrayLength().ShouldBe(0);
    }
}
