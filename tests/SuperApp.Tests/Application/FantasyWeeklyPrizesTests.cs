using Shouldly;
using SuperApp.Application.Features.Fantasy;

namespace SuperApp.Tests.Application;

/// <summary>Owner's rule (2026-11-03): the Super App League's top 3 of each gameweek win 1,500 / 1,000 / 500 Super Points.</summary>
public sealed class FantasyWeeklyPrizesTests
{
    private static TeamToScore Team(long id, int hits = 0) => new(id, Guid.NewGuid(), [], null, hits);

    private static TeamScored Score(long id, int net) => new(id, new TeamScore(net, [], null), net);

    [Fact]
    public void The_three_best_gameweek_scores_win_in_order()
    {
        var teams = new[] { Team(1), Team(2), Team(3), Team(4) };
        var scores = new[] { Score(1, 40), Score(2, 71), Score(3, 55), Score(4, 63) };
        FantasyWeeklyPrizes.TopThree(teams, scores).Select(x => x.Team.TeamId).ShouldBe([2L, 4L, 3L]);
    }

    [Fact]
    public void A_tie_goes_to_fewer_transfer_hits_then_the_older_team()
    {
        var teams = new[] { Team(5, hits: 4), Team(6), Team(7) };
        var scores = new[] { Score(5, 60), Score(6, 60), Score(7, 60) };
        FantasyWeeklyPrizes.TopThree(teams, scores).Select(x => x.Team.TeamId).ShouldBe([6L, 7L, 5L]);
    }

    [Fact]
    public void Default_prizes_are_1500_1000_500() => FantasyWeeklyPrizes.DefaultPoints.ShouldBe([1500, 1000, 500]);
}
