using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Fantasy;

namespace SuperApp.Application.Features.Football;

/// <summary>
/// The football sync job (PRD FR-FAN-01, flow 10.5): season, clubs and squads (weekly), fixtures and live scores,
/// player statistics of finished matches (plus one late re-check for corrections), then scoring of every gameweek
/// whose matches are all over. Stays inside the provider's daily quota; at 100% it stops until the next day.
/// </summary>
public interface IFootballSyncService
{
    Task<IReadOnlyList<SyncResult>> SyncAsync(string? competition, CancellationToken ct);
}

public sealed class FootballSyncService(
    IFootballData data,
    IFootballQuota quota,
    IFantasySyncRepository repository,
    IFantasyScoringService scoring,
    IClock clock,
    ILogger<FootballSyncService> logger) : IFootballSyncService
{
    private const int MaxStatsPerRun = 25;   // a real provider: about one gameweek of matches per run (the simulated one has no limit)
    private static readonly TimeSpan SquadRefresh = TimeSpan.FromDays(7);

    public async Task<IReadOnlyList<SyncResult>> SyncAsync(string? competition, CancellationToken ct)
    {
        var results = new List<SyncResult>();
        foreach (var comp in await repository.CompetitionsAsync(competition, ct))
        {
            try
            {
                results.Add(await SyncOneAsync(comp, ct));
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Football sync of {Competition} failed", comp.Key);
                results.Add(new SyncResult(comp.Key, data.Provider, 0, 0, 0, 0, 0, [], "The football data provider did not answer. Cached data stays on the site."));
            }
        }
        return results;
    }

    private async Task<SyncResult> SyncOneAsync(CompetitionRef comp, CancellationToken ct)
    {
        if (!await quota.CanCallAsync(2, ct))
        {
            return new SyncResult(comp.Key, data.Provider, 0, 0, 0, 0, 0, [], "Today's API-FOOTBALL quota is used up; the sync waits until tomorrow.");
        }
        var season = await data.CurrentSeasonAsync(comp.ExternalLeagueId, ct);
        var seasonId = await repository.EnsureSeasonAsync(comp.Id, season, ct);
        var (clubs, players) = await SyncSquadsAsync(comp, season, seasonId, ct);
        var fixtures = await data.FixturesAsync(comp.ExternalLeagueId, season, ct);
        var gameweeks = await repository.UpsertGameweeksAsync(seasonId, FootballSyncRules.Gameweeks(fixtures, clock.UtcNow), ct);
        var saved = await repository.UpsertFixturesAsync(fixtures, gameweeks, clubs, ct);
        var stats = await SyncStatsAsync(comp, seasonId, ct);
        var scored = new List<int>();
        foreach (var gameweekId in await repository.GameweeksToScoreAsync(seasonId, ct))
        {
            scored.Add(await scoring.ScoreAsync(gameweekId, ct));
        }
        await repository.MarkSyncedAsync(seasonId, clock.UtcNow, ct);
        return new SyncResult(comp.Key, data.Provider, season, clubs.Count, players, saved, stats, scored, null);
    }

    /// <summary>Clubs and squads are read again once a week (one call per club), else the stored ones are used.</summary>
    private async Task<(IReadOnlyDictionary<int, long> Clubs, int Players)> SyncSquadsAsync(CompetitionRef comp, int season, long seasonId, CancellationToken ct)
    {
        if (await repository.SquadsFreshAsync(seasonId, clock.UtcNow - SquadRefresh, ct))
        {
            return (await repository.ClubIdsAsync(seasonId, ct), 0);
        }
        var clubs = await data.ClubsAsync(comp.ExternalLeagueId, season, ct);
        var ids = await repository.UpsertClubsAsync(seasonId, clubs, ct);
        var players = 0;
        foreach (var club in clubs)
        {
            if (!await quota.CanCallAsync(1, ct))
            {
                break;
            }
            players += await repository.UpsertPlayersAsync(await data.SquadAsync(comp.ExternalLeagueId, season, club.ExternalId, ct), ids, ct);
        }
        return (ids, players);
    }

    /// <summary>Reads the statistics of every match that needs them, then saves them in one go.</summary>
    private async Task<int> SyncStatsAsync(CompetitionRef comp, long seasonId, CancellationToken ct)
    {
        var read = new List<(FixtureToStat Fixture, IReadOnlyList<FdPlayerStat> Stats)>();
        foreach (var fixture in await repository.FixturesNeedingStatsAsync(seasonId, clock.UtcNow, data.Provider == "simulated" ? int.MaxValue : MaxStatsPerRun, ct))
        {
            if (!await quota.CanCallAsync(1, ct))
            {
                break;
            }
            read.Add((fixture, await data.FixtureStatsAsync(comp.ExternalLeagueId, fixture.ExternalId, ct)));
        }
        if (read.Count == 0)
        {
            return 0;
        }
        var players = await repository.PlayersByExternalIdAsync(read.SelectMany(r => r.Stats.Select(s => s.PlayerExternalId)).Distinct().ToList(), ct);
        await repository.SaveStatsAsync(read.Select(r => (r.Fixture.FixtureId, Rows(r.Fixture, r.Stats, players))).ToList(), ct);
        return read.Count;
    }

    /// <summary>Statistics rows with clean sheet, goals conceded, bonus (best three ratings) and points.</summary>
    public static IReadOnlyList<StatToSave> Rows(FixtureToStat fixture, IReadOnlyList<FdPlayerStat> stats, IReadOnlyDictionary<int, KnownPlayer> players)
    {
        var known = stats.Where(s => players.ContainsKey(s.PlayerExternalId) && s.Minutes > 0).ToList();
        var bonus = FantasyScoring.Bonus(known.Select(s => (s.PlayerExternalId, s.Rating)).ToList());
        return known.Select(s =>
        {
            var player = players[s.PlayerExternalId];
            var conceded = player.ClubId == fixture.HomeClubId ? fixture.AwayGoals : fixture.HomeGoals;
            var line = new StatLine(s.Minutes, s.Goals, s.Assists, s.Saves, s.OwnGoals, s.PenaltiesSaved, s.PenaltiesMissed, s.YellowCards, s.RedCard,
                conceded == 0 && s.Minutes >= 60, bonus.GetValueOrDefault(s.PlayerExternalId));
            return new StatToSave(player.Id, s.Minutes, s.Goals, s.Assists, s.Saves, s.OwnGoals, s.PenaltiesSaved, s.PenaltiesMissed, s.YellowCards,
                s.RedCard, line.CleanSheet, conceded, line.Bonus, FantasyScoring.PlayerPoints(player.Position, line));
        }).ToList();
    }
}
