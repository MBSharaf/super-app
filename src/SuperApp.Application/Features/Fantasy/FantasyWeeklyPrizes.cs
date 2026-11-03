using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Money;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>One of the gameweek's top 3 in the Super App League and the Super Points they won.</summary>
public sealed record WeeklyWinner(int Place, long TeamId, Guid UserId, int GameweekPoints, int SuperPoints);

/// <summary>
/// The Super App League (owner's rule, 2026-11-03): every league is free, every team is in the Super App League, and the
/// three highest gameweek scores win Super Points after each gameweek — 1,500 / 1,000 / 500 by default (settings
/// <c>fantasy.weekly.first</c>, <c>.second</c>, <c>.third</c>). Given once per team and gameweek, even on a re-score.
/// </summary>
public sealed class FantasyWeeklyPrizes(ILoyaltyStore store, ISettingsReader settings, INotifier notifier, IUnitOfWork unitOfWork, IClock clock)
{
    public static readonly int[] DefaultPoints = [1500, 1000, 500];
    private static readonly string[] Keys = ["fantasy.weekly.first", "fantasy.weekly.second", "fantasy.weekly.third"];

    /// <summary>The top 3 by gameweek points; ties go to the team with fewer transfer hits, then the older team.</summary>
    public static IReadOnlyList<(TeamToScore Team, TeamScored Score)> TopThree(IReadOnlyList<TeamToScore> teams, IReadOnlyList<TeamScored> scores) =>
        teams.Zip(scores).OrderByDescending(x => x.Second.Net).ThenBy(x => x.First.TransfersCost).ThenBy(x => x.First.TeamId).Take(3).ToList();

    public async Task<IReadOnlyList<WeeklyWinner>> AwardAsync(GameweekToScore gameweek, IReadOnlyList<TeamToScore> teams, IReadOnlyList<TeamScored> scores, CancellationToken ct)
    {
        var top = TopThree(teams, scores);
        var winners = new List<WeeklyWinner>();
        for (var i = 0; i < top.Count; i++)
        {
            var points = (int)await settings.DecimalAsync(Keys[i], DefaultPoints[i], ct);
            var (team, score) = top[i];
            winners.Add(new WeeklyWinner(i + 1, team.TeamId, team.UserId, score.Net, points));
            if (points > 0 && !await store.HasPointsForAsync(team.UserId, PointKind.FantasyWeekly, "Gameweek", gameweek.Id, ct))
            {
                store.Add(new PointEntry { UserId = team.UserId, Points = points, Kind = PointKind.FantasyWeekly, RefType = "Gameweek", RefId = gameweek.Id,
                    Reason = $"Super App League: #{i + 1} in gameweek {gameweek.Number}", CreatedAt = clock.UtcNow });
            }
        }
        await unitOfWork.SaveChangesAsync(ct);
        await notifier.NotifyManyAsync(winners.Where(w => w.SuperPoints > 0).Select(w => (w.UserId, new NotificationMessage(NotificationCategory.Fantasy,
            $"You finished #{w.Place} in gameweek {gameweek.Number}", $"{w.SuperPoints:N0} Super Points added for the Super App League.", "#/wallet", NotificationTone.Success))).ToList(), ct);
        return winners;
    }
}
