using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Football sync (simulated provider), public fantasy data, my team, transfers, chips and scoring (API §6.1, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class FantasyTests(FantasyApiFactory api) : IClassFixture<FantasyApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private async Task<JsonElement> BootstrapAsync(ApiClient client, string comp = "EGY")
    {
        await api.EnsureSyncedAsync();
        var (status, body) = await client.GetAsync($"/api/v1/fantasy/{comp}/bootstrap");
        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        return body.Data();
    }

    private static object NewTeam(IEnumerable<JsonElement> squad, string name = "Nile Strikers") => new { name, picks = squad.Select(p => p.Id()).ToArray() };

    [Fact]
    public async Task The_sync_fills_the_cache_scores_played_gameweeks_and_only_admins_can_force_it()
    {
        var (customer, _) = await kit.PersonAsync(partner: false);
        var boot = await BootstrapAsync(customer);
        boot.GetProperty("clubs").GetArrayLength().ShouldBe(18);
        boot.GetProperty("players").GetArrayLength().ShouldBe(18 * 15);
        boot.GetProperty("clubs")[0].GetProperty("logoUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        var competition = boot.GetProperty("competition");
        competition.GetProperty("gameweek").GetInt32().ShouldBe(9);
        competition.GetProperty("lastFinishedGameweek").GetInt32().ShouldBe(8);
        boot.GetProperty("players").EnumerateArray().ShouldContain(p => p.GetProperty("totalPoints").GetInt32() > 0);

        var played = (await customer.GetAsync("/api/v1/fantasy/EGY/fixtures?gameweek=8")).Body.Data();
        played.GetArrayLength().ShouldBe(9);
        played.EnumerateArray().ShouldAllBe(f => f.GetProperty("status").GetString() == "Finished" && f.GetProperty("homeGoals").ValueKind == JsonValueKind.Number);
        (await customer.GetAsync("/api/v1/fantasy/EPL/fixtures?gameweek=9")).Body.Data().EnumerateArray().ShouldAllBe(f => f.GetProperty("status").GetString() == "Scheduled");
        var table = (await customer.GetAsync("/api/v1/fantasy/EGY/table")).Body.Data();
        table.GetArrayLength().ShouldBe(18);
        table[0].GetProperty("played").GetInt32().ShouldBe(8);

        var competitions = (await customer.GetAsync("/api/v1/fantasy/competitions")).Body.Data();
        competitions.EnumerateArray().Select(c => c.GetProperty("key").GetString()).ShouldBe(["egy", "epl"]);
        (await customer.GetAsync("/api/v1/fantasy/XYZ/bootstrap")).Body.Code().ShouldBe("no_season");

        (await customer.PostAsync("/api/v1/admin/fantasy/sync")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await (await api.ClientAsync()).PostAsync("/api/v1/admin/fantasy/sync")).Status.ShouldBe(HttpStatusCode.Unauthorized);
        var ceo = await api.CeoAsync();
        var again = (await ceo.PostAsync("/api/v1/admin/fantasy/sync?competition=EGY")).Body.Data();
        again[0].GetProperty("players").GetInt32().ShouldBe(0);   // squads are read once a week
        var usage = (await ceo.GetAsync("/api/v1/admin/fantasy/usage")).Body.Data();
        (usage.GetProperty("provider").GetString(), usage.GetProperty("level").GetString(), usage.GetProperty("limit").GetInt32()).ShouldBe(("simulated", "green", 100));
        (await api.DbAsync(db => db.Set<OutboxEvent>().CountAsync(e => e.Type == "fantasy.gameweek.finished"))).ShouldBe(16);   // 8 played gameweeks × 2 leagues, once each
    }

    [Fact]
    public async Task Players_can_be_filtered_sorted_and_opened()
    {
        var client = await api.ClientAsync();
        await api.EnsureSyncedAsync();
        var page = (await client.GetAsync("/api/v1/fantasy/EPL/players?position=FWD&sort=price&pageSize=5")).Body;
        page.Data().GetArrayLength().ShouldBe(5);
        page.GetProperty("meta").GetProperty("total").GetInt32().ShouldBe(20 * 3);
        page.Data()[0].GetProperty("name").GetString().ShouldBe("Erling Haaland");
        var salah = (await client.GetAsync("/api/v1/fantasy/EPL/players?q=salah")).Body.Data()[0];
        var detail = (await client.GetAsync($"/api/v1/fantasy/EPL/players/{salah.Id()}")).Body.Data();
        detail.GetProperty("upcoming").GetArrayLength().ShouldBe(5);
        detail.GetProperty("player").GetProperty("price").GetDecimal().ShouldBe(13.0m);
        (await client.GetAsync("/api/v1/fantasy/EPL/players/999999999")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_squad_must_follow_the_rules_and_one_team_per_league()
    {
        var (client, _) = await kit.PersonAsync(partner: false);
        var boot = await BootstrapAsync(client);
        var squad = FantasyKit.CheapSquad(boot);
        var players = boot.GetProperty("players").EnumerateArray().ToList();

        (await (await api.ClientAsync()).PostAsync("/api/v1/fantasy/EGY/team", NewTeam(squad))).Status.ShouldBe(HttpStatusCode.Unauthorized);
        (await client.PostAsync("/api/v1/fantasy/EGY/team", NewTeam(squad.Take(14)))).Status.ShouldBe(HttpStatusCode.BadRequest);
        var rich = new[] { ("GK", 2), ("DEF", 5), ("MID", 5), ("FWD", 3) }.SelectMany(s =>
            players.Where(p => p.GetProperty("position").GetString() == s.Item1).OrderByDescending(p => p.GetProperty("price").GetDecimal()).Take(s.Item2));
        var tooRich = await client.PostAsync("/api/v1/fantasy/EGY/team", NewTeam(rich));
        tooRich.Body.Code().ShouldBeOneOf("over_budget", "max_per_club");
        var ahly = players.Where(p => p.GetProperty("clubId").GetInt64() == players.First(x => x.GetProperty("name").GetString() == "Trezeguet").GetProperty("clubId").GetInt64()
            && p.GetProperty("position").GetString() == "DEF").Take(4).ToList();
        var club = squad.Where(p => p.GetProperty("position").GetString() != "DEF").Concat(ahly).Append(squad.First(p => p.GetProperty("position").GetString() == "DEF" && ahly.All(a => a.Id() != p.Id())));
        (await client.PostAsync("/api/v1/fantasy/EGY/team", NewTeam(club))).Body.Code().ShouldBe("max_per_club");
        var shape = squad.Skip(1).Append(players.First(p => p.GetProperty("position").GetString() == "FWD" && squad.All(s => s.Id() != p.Id())));
        (await client.PostAsync("/api/v1/fantasy/EGY/team", NewTeam(shape))).Body.Code().ShouldBe("formation");

        var created = await client.PostAsync("/api/v1/fantasy/EGY/team", NewTeam(squad));
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var team = created.Body.Data();
        team.GetProperty("gameweek").GetInt32().ShouldBe(9);
        team.GetProperty("picks").GetArrayLength().ShouldBe(15);
        team.GetProperty("bank").GetDecimal().ShouldBe(100m - squad.Sum(p => p.GetProperty("price").GetDecimal()));
        team.GetProperty("picks").EnumerateArray().Count(p => p.GetProperty("isCaptain").GetBoolean()).ShouldBe(1);
        (await client.PostAsync("/api/v1/fantasy/EGY/team", NewTeam(squad))).Body.Code().ShouldBe("team_exists");
        (await client.GetAsync("/api/v1/fantasy/EGY/team")).Body.Data().GetProperty("name").GetString().ShouldBe("Nile Strikers");
        (await client.GetAsync("/api/v1/fantasy/EPL/team")).Body.Code().ShouldBe("no_team");
        (await client.SendAsync(HttpMethod.Patch, "/api/v1/fantasy/EGY/team", new { name = "Cairo Kings" })).Body.Data().GetProperty("name").GetString().ShouldBe("Cairo Kings");
        (await api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.Title == "Your fantasy team is ready"))).ShouldBeTrue();
    }

    [Fact]
    public async Task The_lineup_keeps_a_valid_formation()
    {
        var (client, _) = await kit.PersonAsync(partner: false);
        var squad = FantasyKit.CheapSquad(await BootstrapAsync(client)).Select(p => p.Id()).ToList();
        (await client.PostAsync("/api/v1/fantasy/EGY/team", new { name = "Lineup FC", picks = squad })).Status.ShouldBe(HttpStatusCode.Created);
        // squad order: GK 0-1, DEF 2-6, MID 7-11, FWD 12-14 → 3-5-2 with the second keeper first on the bench
        var starters = new[] { squad[0], squad[2], squad[3], squad[4], squad[7], squad[8], squad[9], squad[10], squad[11], squad[12], squad[13] };
        var bench = new[] { squad[1], squad[5], squad[6], squad[14] };
        var ok = await client.SendAsync(HttpMethod.Put, "/api/v1/fantasy/EGY/team/lineup", new { starters, bench, captainId = squad[12], viceCaptainId = squad[7] });
        ok.Status.ShouldBe(HttpStatusCode.OK, ok.Body.ToString());
        var captain = ok.Body.Data().GetProperty("picks").EnumerateArray().Single(p => p.GetProperty("isCaptain").GetBoolean());
        (captain.GetProperty("playerId").GetInt64(), captain.GetProperty("slot").GetInt32()).ShouldBe((squad[12], 10));

        var keepers = new[] { squad[0], squad[1], squad[3], squad[4], squad[7], squad[8], squad[9], squad[10], squad[11], squad[12], squad[13] };
        var keepersBench = new[] { squad[2], squad[5], squad[6], squad[14] };
        (await client.SendAsync(HttpMethod.Put, "/api/v1/fantasy/EGY/team/lineup", new { starters = keepers, bench = keepersBench, captainId = squad[12], viceCaptainId = squad[7] }))
            .Body.Code().ShouldBe("formation");
        (await client.SendAsync(HttpMethod.Put, "/api/v1/fantasy/EGY/team/lineup", new { starters, bench, captainId = squad[1], viceCaptainId = squad[7] }))
            .Body.Code().ShouldBe("captain");
        (await client.SendAsync(HttpMethod.Put, "/api/v1/fantasy/EGY/team/lineup", new { starters = starters.Take(10), bench, captainId = squad[12], viceCaptainId = squad[7] }))
            .Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Transfers_cost_points_after_the_free_one_and_chips_are_played_once()
    {
        var (client, userId) = await kit.PersonAsync(partner: false);
        var boot = await BootstrapAsync(client);
        var squad = FantasyKit.CheapSquad(boot);
        (await client.PostAsync("/api/v1/fantasy/EGY/team", NewTeam(squad))).Status.ShouldBe(HttpStatusCode.Created);
        // spares from clubs we have nobody from, so the 3-per-club rule never gets in the way
        var outside = boot.GetProperty("players").EnumerateArray().Where(p => squad.All(s => s.GetProperty("clubId").GetInt64() != p.GetProperty("clubId").GetInt64()))
            .OrderBy(p => p.GetProperty("price").GetDecimal()).ToList();
        long Spare(string position, int skip = 0) => outside.Where(p => p.GetProperty("position").GetString() == position).Skip(skip).First().Id();
        long Mine(string position, int skip = 0) => squad.Where(p => p.GetProperty("position").GetString() == position).Skip(skip).First().Id();

        (await client.PostAsync("/api/v1/fantasy/EGY/team/transfers", new { transfers = new[] { new { @out = Mine("GK"), @in = Spare("FWD") } } })).Body.Code().ShouldBe("transfer_position");
        var first = (await client.PostAsync("/api/v1/fantasy/EGY/team/transfers", new { transfers = new[] { new { @out = Mine("MID"), @in = Spare("MID") } } })).Body.Data();
        (first.GetProperty("hits").GetInt32(), first.GetProperty("team").GetProperty("freeTransfers").GetInt32()).ShouldBe((0, 0));
        var second = (await client.PostAsync("/api/v1/fantasy/EGY/team/transfers", new { transfers = new[] { new { @out = Mine("FWD"), @in = Spare("FWD") } } })).Body.Data();
        (second.GetProperty("hits").GetInt32(), second.GetProperty("team").GetProperty("transfersCost").GetInt32()).ShouldBe((4, 4));
        await api.DbAsync(db => db.Set<FantasyTeam>().Where(t => t.UserId == userId).ExecuteUpdateAsync(u => u.SetProperty(t => t.Bank, 0m)));
        var rich = boot.GetProperty("players").EnumerateArray().Where(p => p.GetProperty("position").GetString() == "MID").MaxBy(p => p.GetProperty("price").GetDecimal()).Id();
        (await client.PostAsync("/api/v1/fantasy/EGY/team/transfers", new { transfers = new[] { new { @out = Mine("MID", 1), @in = rich } } })).Body.Code().ShouldBe("over_budget");
        await api.DbAsync(db => db.Set<FantasyTeam>().Where(t => t.UserId == userId).ExecuteUpdateAsync(u => u.SetProperty(t => t.Bank, 20m)));

        (await client.PostAsync("/api/v1/fantasy/EGY/team/chips/wildcard")).Body.Data().GetProperty("activeChip").GetString().ShouldBe("wildcard");
        var free = (await client.PostAsync("/api/v1/fantasy/EGY/team/transfers", new { transfers = new[] { new { @out = Mine("DEF"), @in = Spare("DEF") } } })).Body.Data();
        free.GetProperty("hits").GetInt32().ShouldBe(0);
        (await client.PostAsync("/api/v1/fantasy/EGY/team/chips/benchboost")).Body.Code().ShouldBe("chip_this_week");
        (await client.PostAsync("/api/v1/fantasy/EGY/team/chips/wildcard")).Body.Code().ShouldBe("chip_used");
        (await client.PostAsync("/api/v1/fantasy/EGY/team/chips/freehit")).Body.Code().ShouldBe("unknown_chip");
    }

    [Fact]
    public async Task The_fantasy_kill_switch_stops_changes()
    {
        var (client, _) = await kit.PersonAsync(partner: false);
        var squad = FantasyKit.CheapSquad(await BootstrapAsync(client));
        await api.DbAsync(db => db.Set<FeatureFlag>().Where(f => f.Key == "kill.fantasy").ExecuteUpdateAsync(u => u.SetProperty(f => f.IsEnabled, false)));
        try
        {
            (await client.PostAsync("/api/v1/fantasy/EGY/team", NewTeam(squad))).Status.ShouldBe((HttpStatusCode)423);
        }
        finally
        {
            await api.DbAsync(db => db.Set<FeatureFlag>().Where(f => f.Key == "kill.fantasy").ExecuteUpdateAsync(u => u.SetProperty(f => f.IsEnabled, true)));
        }
    }
}
