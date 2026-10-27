using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Scoring of a finished gameweek on its own database (it moves gameweek 9 into the past).</summary>
[Trait("Category", "Database")]
public sealed class FantasyScoringTests(FantasyApiFactory api) : IClassFixture<FantasyApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private async Task<JsonElement> BootstrapAsync(ApiClient client)
    {
        await api.EnsureSyncedAsync();
        return (await client.GetAsync("/api/v1/fantasy/EPL/bootstrap")).Body.Data();
    }

    [Fact]
    public async Task A_finished_gameweek_is_scored_with_captain_and_hits_and_managers_are_told()
    {
        var (client, userId) = await kit.PersonAsync(partner: false);
        var squad = FantasyKit.CheapSquad(await BootstrapAsync(client)).Select(p => p.Id()).ToList();
        var team = (await client.PostAsync("/api/v1/fantasy/EPL/team", new { name = "Scorers", picks = squad })).Body.Data();
        var captain = team.GetProperty("picks").EnumerateArray().Single(p => p.GetProperty("isCaptain").GetBoolean()).GetProperty("playerId").GetInt64();
        var starters = team.GetProperty("picks").EnumerateArray().Where(p => p.GetProperty("slot").GetInt32() <= 11).Select(p => p.GetProperty("playerId").GetInt64()).ToList();

        await PlayGameweekNineAsync(starters, captain);
        var ceo = await api.CeoAsync();
        (await ceo.PostAsync("/api/v1/admin/fantasy/rescore/9?competition=EPL")).Status.ShouldBe(HttpStatusCode.OK);

        var history = (await client.GetAsync("/api/v1/fantasy/EPL/team/history")).Body.Data();
        var nine = history.GetProperty("gameweeks").EnumerateArray().Single(g => g.GetProperty("gameweek").GetInt32() == 9);
        nine.GetProperty("points").GetInt32().ShouldBe(10 * 2 + 2 * 6);   // ten starters with 2, the captain's 6 doubled (90 min + goal)
        history.GetProperty("totalPoints").GetInt32().ShouldBe(32);
        history.GetProperty("overallRank").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        (await api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.UserId == userId && n.Title == "Gameweek 9 is complete"))).ShouldBeTrue();
        (await api.DbAsync(db => db.Set<OutboxEvent>().AnyAsync(e => e.Type == "fantasy.gameweek.finished"))).ShouldBeTrue();
        (await ceo.PostAsync("/api/v1/admin/fantasy/rescore/10?competition=EPL")).Body.Code().ShouldBe("not_finished");
    }

    /// <summary>Gameweek 9 played out in the database: every starter 90 minutes (2 points), the captain a goal (6 points stored).</summary>
    private Task<int> PlayGameweekNineAsync(List<long> starters, long captain) => api.DbAsync(async db =>
    {
        var gameweek = await db.Set<Gameweek>().Include(g => g.Fixtures).SingleAsync(g => g.Number == 9 && g.Season.IsCurrent && g.Season.Competition.Key == "EPL");
        foreach (var fixture in gameweek.Fixtures)
        {
            (fixture.Status, fixture.HomeGoals, fixture.AwayGoals) = (FixtureStatus.Finished, 1, 1);
        }
        gameweek.Deadline = DateTime.UtcNow.AddDays(-1);
        var clubs = await db.Set<Player>().Where(p => starters.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ClubId);
        foreach (var id in starters)
        {
            var fixture = gameweek.Fixtures.First(f => f.HomeClubId == clubs[id] || f.AwayClubId == clubs[id]);
            var goal = id == captain;
            db.Set<PlayerFixtureStat>().Add(new PlayerFixtureStat { PlayerId = id, FixtureId = fixture.Id, Minutes = 90, Goals = (byte)(goal ? 1 : 0), GoalsConceded = 1, Points = (short)(goal ? 6 : 2) });
        }
        return await db.SaveChangesAsync();
    });
}
