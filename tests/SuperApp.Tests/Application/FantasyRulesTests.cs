using Shouldly;
using SuperApp.Application.Features.Fantasy;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Application;

/// <summary>Fantasy scoring, squad, lineup and transfer rules, and the sync's gameweek rules — without a database.</summary>
public sealed class FantasyRulesTests
{
    private static StatLine Line(int minutes = 90, int goals = 0, int assists = 0, int saves = 0, bool cleanSheet = false, int yellow = 0, bool red = false,
        int ownGoals = 0, int penSaved = 0, int penMissed = 0, int bonus = 0) =>
        new(minutes, goals, assists, saves, ownGoals, penSaved, penMissed, yellow, red, cleanSheet, bonus);

    [Fact]
    public void Player_points_follow_the_rules_table()
    {
        FantasyScoring.PlayerPoints(PlayerPosition.Forward, Line(minutes: 0, goals: 1)).ShouldBe(0);
        FantasyScoring.PlayerPoints(PlayerPosition.Forward, Line(minutes: 59)).ShouldBe(1);
        FantasyScoring.PlayerPoints(PlayerPosition.Forward, Line(goals: 2, assists: 1)).ShouldBe(2 + 8 + 3);
        FantasyScoring.PlayerPoints(PlayerPosition.Midfielder, Line(goals: 1, cleanSheet: true)).ShouldBe(2 + 5 + 1);
        FantasyScoring.PlayerPoints(PlayerPosition.Defender, Line(goals: 1, cleanSheet: true, yellow: 1)).ShouldBe(2 + 6 + 4 - 1);
        FantasyScoring.PlayerPoints(PlayerPosition.Defender, Line(minutes: 45, cleanSheet: true)).ShouldBe(1);   // clean sheet needs 60 minutes
        FantasyScoring.PlayerPoints(PlayerPosition.Goalkeeper, Line(saves: 7, cleanSheet: true, penSaved: 1, bonus: 3)).ShouldBe(2 + 2 + 4 + 5 + 3);
        FantasyScoring.PlayerPoints(PlayerPosition.Forward, Line(red: true, ownGoals: 1, penMissed: 1)).ShouldBe(2 - 3 - 2 - 2);
    }

    [Fact]
    public void Bonus_goes_to_the_three_best_ratings_and_ties_share()
    {
        var bonus = FantasyScoring.Bonus([(1, 8.1m), (2, 7.5m), (3, 7.5m), (4, 7.0m), (5, 0m)]);
        bonus[1].ShouldBe(3);
        bonus[2].ShouldBe(2);
        bonus[3].ShouldBe(2);
        bonus.ContainsKey(4).ShouldBeFalse();
        bonus.ContainsKey(5).ShouldBeFalse();
    }

    /// <summary>Slots 1–11: GK, 4 DEF, 4 MID, 2 FWD (ids 1–11); bench 12 GK, 13 DEF, 14 MID, 15 FWD. Captain 10, vice 9.</summary>
    private static List<ScoringPick> Team()
    {
        PlayerPosition[] xi = [PlayerPosition.Goalkeeper, .. Enumerable.Repeat(PlayerPosition.Defender, 4), .. Enumerable.Repeat(PlayerPosition.Midfielder, 4),
            PlayerPosition.Forward, PlayerPosition.Forward];
        PlayerPosition[] bench = [PlayerPosition.Goalkeeper, PlayerPosition.Defender, PlayerPosition.Midfielder, PlayerPosition.Forward];
        return xi.Concat(bench).Select((p, i) => new ScoringPick(i + 1, i + 1, p, i + 1 == 10, i + 1 == 9)).ToList();
    }

    private static Dictionary<long, PlayerGameweek> AllPlayed(int points = 2) => Enumerable.Range(1, 15).ToDictionary(i => (long)i, _ => new PlayerGameweek(90, points));

    [Fact]
    public void Team_points_double_the_captain_and_leave_the_bench_out()
    {
        var players = AllPlayed();
        players[10] = new PlayerGameweek(90, 9);
        var score = FantasyScoring.TeamPoints(Team(), players, null);
        score.Total.ShouldBe(10 * 2 + 18);
        score.CaptainUsed.ShouldBe(10);
        score.Picks.Where(p => p.Slot > 11).ShouldAllBe(p => p.Multiplier == 0);
    }

    [Fact]
    public void A_starter_who_did_not_play_is_replaced_from_the_bench_and_the_vice_takes_the_armband()
    {
        var players = AllPlayed();
        players[10] = new PlayerGameweek(0, 0);   // captain (forward) did not play
        players[13] = new PlayerGameweek(0, 0);   // first outfield sub did not play either
        players[14] = new PlayerGameweek(90, 5);
        var score = FantasyScoring.TeamPoints(Team(), players, null);
        score.CaptainUsed.ShouldBe(9);
        score.Picks.Single(p => p.PlayerId == 14).Multiplier.ShouldBe(1);   // MID for FWD keeps 1 forward on the pitch
        score.Total.ShouldBe(10 * 2 + 2 + 5);   // 10 starters at 2, the vice doubled (+2), the sub's 5
    }

    [Fact]
    public void A_goalkeeper_is_only_replaced_by_the_substitute_goalkeeper()
    {
        var players = AllPlayed();
        players[1] = new PlayerGameweek(0, 0);
        players[12] = new PlayerGameweek(0, 0);
        var score = FantasyScoring.TeamPoints(Team(), players, null);
        score.Picks.Where(p => p.Slot > 11).ShouldAllBe(p => p.Multiplier == 0);
        score.Total.ShouldBe(10 * 2 + 2);
    }

    [Fact]
    public void Bench_boost_counts_all_fifteen_and_triple_captain_triples()
    {
        FantasyScoring.TeamPoints(Team(), AllPlayed(), FantasyScoring.BenchBoostChip).Total.ShouldBe(15 * 2 + 2);
        FantasyScoring.TeamPoints(Team(), AllPlayed(), FantasyScoring.TripleCaptainChip).Total.ShouldBe(11 * 2 + 4);
    }

    private static List<SquadPlayer> Squad(decimal price = 6m)
    {
        var shape = new[] { (PlayerPosition.Goalkeeper, 2), (PlayerPosition.Defender, 5), (PlayerPosition.Midfielder, 5), (PlayerPosition.Forward, 3) };
        var id = 0;
        return shape.SelectMany(s => Enumerable.Range(0, s.Item2).Select(_ => { id++; return new SquadPlayer(id, id, s.Item1, price); })).ToList();
    }

    [Fact]
    public void Squad_rules_check_size_shape_clubs_and_budget()
    {
        SquadRules.CheckSquad(Squad(), 100, 3).ShouldBeNull();
        SquadRules.CheckSquad(Squad().Take(14).ToList(), 100, 3)!.Value.Code.ShouldBe("squad_size");
        SquadRules.CheckSquad(Squad(7m), 100, 3)!.Value.Code.ShouldBe("over_budget");
        var club = Squad().Select((p, i) => i < 4 ? p with { ClubId = 99 } : p).ToList();
        SquadRules.CheckSquad(club, 100, 3)!.Value.Code.ShouldBe("max_per_club");
        var shape = Squad().Select((p, i) => i == 0 ? p with { Position = PlayerPosition.Forward } : p).ToList();
        SquadRules.CheckSquad(shape, 100, 3)!.Value.Code.ShouldBe("formation");
    }

    [Fact]
    public void Default_lineup_is_valid_and_lineup_rules_hold()
    {
        var squad = Squad();
        var byId = squad.ToDictionary(p => p.Id);
        var lineup = SquadRules.DefaultLineup(squad);
        SquadRules.CheckLineup(lineup, byId).ShouldBeNull();
        squad.Single(p => p.Id == lineup.Bench[0]).Position.ShouldBe(PlayerPosition.Goalkeeper);

        var twoKeepers = lineup with { Starters = [.. lineup.Starters.Take(10), lineup.Bench[0]], Bench = [lineup.Starters[10], .. lineup.Bench.Skip(1)] };
        SquadRules.CheckLineup(twoKeepers, byId)!.Value.Code.ShouldBe("formation");
        SquadRules.CheckLineup(lineup with { ViceCaptainId = lineup.CaptainId }, byId)!.Value.Code.ShouldBe("captain");
        SquadRules.CheckLineup(lineup with { CaptainId = lineup.Bench[1] }, byId)!.Value.Code.ShouldBe("captain");
    }

    [Fact]
    public void Transfers_cost_four_points_after_the_free_ones_and_nothing_with_a_wildcard()
    {
        SquadRules.Hits(1, 1, false).ShouldBe(0);
        SquadRules.Hits(3, 1, false).ShouldBe(8);
        SquadRules.Hits(3, 2, false).ShouldBe(4);
        SquadRules.Hits(5, 0, true).ShouldBe(0);
        var lineup = new Lineup([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11], [12, 13, 14, 15], 10, 9);
        var after = SquadRules.ApplyTransfers(lineup, [(10, 99), (13, 77)]);
        after.Starters[9].ShouldBe(99);
        after.CaptainId.ShouldBe(99);
        after.Bench[1].ShouldBe(77);
    }

    [Fact]
    public void Gameweeks_get_their_deadline_and_status_from_the_fixtures()
    {
        var now = new DateTime(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        FdFixture F(int round, DateTime kickoff, FixtureStatus status) => new(round * 10 + kickoff.Day, round, 1, 2, kickoff, status, null, null, null);
        var plans = FootballSyncRules.Gameweeks(
        [
            F(1, now.AddDays(-8), FixtureStatus.Finished), F(1, now.AddDays(-7), FixtureStatus.Postponed),
            F(2, now.AddHours(-1), FixtureStatus.Live), F(2, now.AddDays(1), FixtureStatus.Scheduled),
            F(3, now.AddDays(7), FixtureStatus.Scheduled),
        ], now);
        plans.Select(p => p.Status).ShouldBe([GameweekStatus.Scoring, GameweekStatus.Live, GameweekStatus.Upcoming]);
        plans[2].Deadline.ShouldBe(now.AddDays(7).AddMinutes(-90));
    }

    [Fact]
    public void Sync_rows_add_clean_sheets_conceded_goals_bonus_and_points()
    {
        var fixture = new FixtureToStat(1, 100, HomeClubId: 10, AwayClubId: 20, HomeGoals: 2, AwayGoals: 0);
        var players = new Dictionary<int, KnownPlayer> { [1] = new(11, 10, PlayerPosition.Defender), [2] = new(22, 20, PlayerPosition.Forward) };
        var rows = FootballSyncService.Rows(fixture,
        [
            new FdPlayerStat(1, 10, 90, 1, 0, 0, 0, 0, 0, 0, false, 8.0m),
            new FdPlayerStat(2, 20, 90, 0, 0, 0, 0, 0, 0, 1, false, 6.5m),
            new FdPlayerStat(3, 20, 90, 0, 0, 0, 0, 0, 0, 0, false, 9.0m),   // not in our tables → skipped
        ], players);
        rows.Count.ShouldBe(2);
        var defender = rows.Single(r => r.PlayerId == 11);
        (defender.CleanSheet, defender.GoalsConceded, defender.Bonus, defender.Points).ShouldBe((true, 0, 3, 2 + 6 + 4 + 3));
        var forward = rows.Single(r => r.PlayerId == 22);
        (forward.CleanSheet, forward.GoalsConceded, forward.Bonus, forward.Points).ShouldBe((false, 2, 2, 2 - 1 + 2));
    }
}
