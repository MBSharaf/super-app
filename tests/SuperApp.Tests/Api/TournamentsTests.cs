using System.Net;
using System.Text.Json;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>A venue runs a cup: hours reserved, teams pay and register, the draw, results to a champion; a cancel refunds.</summary>
[Trait("Category", "Database")]
public sealed class TournamentsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private static object Cup(long pitchId, string type, int teams, int days) => new
    {
        pitchId, name = $"Test {type} {Guid.NewGuid():N}"[..20], type, teamCount = teams, entryFee = 500, prizeText = "Trophy",
        startsOn = PartnerKit.Day(days), hour = 18, rulesText = "Two halves of 20 minutes, penalties if level.",
    };

    private async Task<ApiClient> TeamAsync(long id, string name)
    {
        var (player, _) = await kit.PersonAsync(partner: false);
        var r = await player.PostAsync($"/api/v1/tournaments/{id}/teams", new { teamName = name, phone = "+20 100 123 4567", acceptWaiver = true });
        r.Status.ShouldBe(HttpStatusCode.Created, r.Body.ToString());
        return player;
    }

    private static decimal Wallet(JsonElement wallets, string type) =>
        wallets.EnumerateArray().Where(w => w.GetProperty("type").GetString() == type).Select(w => w.GetProperty("available").GetDecimal()).FirstOrDefault();

    [Fact]
    public async Task A_cup_reserves_its_hours_takes_paid_teams_and_ends_with_a_champion()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var pitchId = pitch.GetProperty("id").GetInt64();
        (await partner.PostAsync("/api/v1/partner/tournaments", Cup(pitchId, "knockout", 4, 1))).Body.Code().ShouldBe("too_soon");
        var made = await partner.PostAsync("/api/v1/partner/tournaments", Cup(pitchId, "knockout", 4, 4));
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var id = made.Body.Data().GetProperty("id").GetInt64();
        made.Body.Data().GetProperty("matches").GetArrayLength().ShouldBe(3);

        var (other, _) = await kit.PersonAsync(partner: false);
        (await other.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(4), hour = 18, duration = 1, payAtVenue = false, acceptWaiver = true }))
            .Status.ShouldBe(HttpStatusCode.Conflict);

        var first = await TeamAsync(id, "Maadi Falcons");
        (await first.PostAsync($"/api/v1/tournaments/{id}/teams", new { teamName = "Again FC", phone = "+20 100 123 4567", acceptWaiver = true })).Body.Code().ShouldBe("already_registered");
        var mine = (await first.GetAsync($"/api/v1/tournaments/{id}")).Body.Data().GetProperty("teams").EnumerateArray().Single(t => t.GetProperty("mine").GetBoolean());
        mine.GetProperty("paid").GetBoolean().ShouldBeTrue();
        (await first.PostAsync($"/api/v1/tournaments/{id}/teams/{mine.GetProperty("id").GetInt64()}/withdraw")).Status.ShouldBe(HttpStatusCode.OK);
        Wallet((await first.GetAsync("/api/v1/wallet")).Body.Data(), "Customer").ShouldBe(500);

        foreach (var name in new[] { "Dokki Dynamos", "Zayed United", "Giza Pharaohs" })
        {
            await TeamAsync(id, name);
        }
        (await partner.PostAsync($"/api/v1/partner/tournaments/{id}/teams", new { teamName = "Venue Kids", phone = (string?)null })).Status.ShouldBe(HttpStatusCode.OK);
        var (late, _) = await kit.PersonAsync(partner: false);
        (await late.PostAsync($"/api/v1/tournaments/{id}/teams", new { teamName = "Late FC", phone = "+20 100 123 4567", acceptWaiver = true })).Body.Code().ShouldBe("full");

        var drawn = (await partner.PostAsync($"/api/v1/partner/tournaments/{id}/draw")).Body.Data();
        drawn.GetProperty("status").GetString().ShouldBe("running");
        var matches = drawn.GetProperty("matches").EnumerateArray().ToList();
        var (semi1, semi2, final) = (matches[0], matches[1], matches[2]);
        await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/tournaments/{id}/matches/{semi1.GetProperty("id").GetInt64()}", new { homeGoals = 2, awayGoals = 1 });
        (await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/tournaments/{id}/matches/{semi2.GetProperty("id").GetInt64()}", new { homeGoals = 1, awayGoals = 1 })).Body.Code().ShouldBe("pens_winner");
        var away = semi2.GetProperty("awayTeamId").GetInt64();
        await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/tournaments/{id}/matches/{semi2.GetProperty("id").GetInt64()}", new { homeGoals = 1, awayGoals = 1, pensWinnerId = away });
        var done = (await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/tournaments/{id}/matches/{final.GetProperty("id").GetInt64()}", new { homeGoals = 0, awayGoals = 3 })).Body.Data();
        done.GetProperty("status").GetString().ShouldBe("finished");
        done.GetProperty("championTeamId").GetInt64().ShouldBe(away);
        (await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/tournaments/{id}/matches/{semi1.GetProperty("id").GetInt64()}", new { homeGoals = 0, awayGoals = 1 }))
            .Body.Code().ShouldBe("later_round_played");
    }

    [Fact]
    public async Task Cancelling_a_league_refunds_every_paid_team_and_frees_the_hours()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = (await partner.PostAsync("/api/v1/partner/tournaments", Cup(pitch.GetProperty("id").GetInt64(), "league", 3, 5))).Body.Data().GetProperty("id").GetInt64();
        var captains = new List<ApiClient>();
        foreach (var name in new[] { "Shoubra Stars", "Rehab Rockets" })
        {
            captains.Add(await TeamAsync(id, name));
        }
        (await partner.PostAsync($"/api/v1/partner/tournaments/{id}/cancel")).Body.Data().GetProperty("status").GetString().ShouldBe("cancelled");
        foreach (var c in captains)
        {
            Wallet((await c.GetAsync("/api/v1/wallet")).Body.Data(), "Customer").ShouldBe(500);
        }
        var (player, _) = await kit.PersonAsync(partner: false);
        (await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(5), hour = 18, duration = 1, payAtVenue = false, acceptWaiver = true }))
            .Status.ShouldBe(HttpStatusCode.Created);
        (await player.GetAsync($"/api/v1/tournaments/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }
}
