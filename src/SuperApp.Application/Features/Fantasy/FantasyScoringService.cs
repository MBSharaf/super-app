using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>
/// The scoring job (PRD FR-FAN-04, FR-FAN-05): player points are already on the statistics rows; this adds them up per
/// team (automatic substitutions, captain, chips, transfer hits), then totals, ranks and group tables. A re-score after
/// the provider's corrections writes the new numbers without telling everyone again.
/// </summary>
public sealed class FantasyScoringService(
    IFantasyScoringRepository repository,
    INotifier notifier,
    IAuditLogger audit) : IFantasyScoringService
{
    public async Task<int> ScoreAsync(long gameweekId, CancellationToken ct)
    {
        var gameweek = await repository.GameweekAsync(gameweekId, ct) ?? throw new NotFoundException("Gameweek not found.");
        await repository.CarryPicksForwardAsync(gameweek, ct);
        var players = await repository.PlayerTotalsAsync(gameweekId, ct);
        var teams = await repository.TeamsAsync(gameweekId, ct);
        var scores = teams.Select(t =>
        {
            var score = FantasyScoring.TeamPoints(t.Picks, players, t.Chip);
            return new TeamScored(t.TeamId, score, score.Total - t.TransfersCost);
        }).ToList();
        await repository.SaveAsync(gameweek, scores, ct);
        if (gameweek.ScoredAt is null)
        {
            await TellManagersAsync(gameweek, teams, scores, ct);
            await repository.QueueEventAsync("fantasy.gameweek.finished", new { competition = gameweek.Competition, gameweek = gameweek.Number, teams = teams.Count }, ct);
        }
        await audit.LogAsync(AuditSystem.ApiOrAgent, gameweek.ScoredAt is null ? "fantasy.scored" : "fantasy.rescored",
            $"{gameweek.Competition} gameweek {gameweek.Number}: {teams.Count} team(s)", "Gameweek", gameweek.Id.ToString(), ct: ct);
        return gameweek.Number;
    }

    private Task TellManagersAsync(GameweekToScore gameweek, IReadOnlyList<TeamToScore> teams, IReadOnlyList<TeamScored> scores, CancellationToken ct) =>
        notifier.NotifyManyAsync(teams.Zip(scores).Select(x => (x.First.UserId, new NotificationMessage(NotificationCategory.Fantasy,
            $"Gameweek {gameweek.Number} is complete", $"You scored {x.Second.Net} points.", "#/fantasy", NotificationTone.Success))).ToList(), ct);
}
