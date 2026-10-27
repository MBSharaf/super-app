using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Fantasy;

namespace SuperApp.Application.Features.Football;

/// <summary>
/// The football sync job (PRD FR-FAN-01, flow 10.5), per competition with its own provider: season, fixtures and scores
/// (the whole season once a day, else only the rounds around today), clubs; for fantasy competitions also squads
/// (weekly), player statistics of finished matches (plus one late re-check for corrections) and scoring of every
/// gameweek whose matches are all over. API-FOOTBALL stays inside its daily quota; at 100% it stops until the next day.
/// </summary>
public interface IFootballSyncService
{
    Task<IReadOnlyList<SyncResult>> SyncAsync(string? competition, CancellationToken ct);
}

public sealed class FootballSyncService(
    IFootballSources sources,
    IFootballQuota quota,
    IFantasySyncRepository repository,
    IFantasyScoringService scoring,
    IClock clock,
    ILogger<FootballSyncService> logger) : IFootballSyncService
{
    private const int MaxStatsPerRun = 25;   // a real provider: about one gameweek of matches per run (the simulated one has no limit)
    private const string ApiFootball = "api-football";
    private static readonly TimeSpan SquadRefresh = TimeSpan.FromDays(7);
    private static readonly TimeSpan FullRefresh = TimeSpan.FromDays(1);

    public async Task<IReadOnlyList<SyncResult>> SyncAsync(string? competition, CancellationToken ct)
    {
        var results = new List<SyncResult>();
        foreach (var comp in await repository.CompetitionsAsync(competition, ct))
        {
            var data = sources.For(comp);
            try
            {
                results.Add(data is null ? Skipped(comp, "offline", "No data source can be read for this competition right now.") : await SyncOneAsync(data, comp, ct));
            }
            catch (HttpRequestException ex)
            {
                logger.LogWarning(ex, "Football sync of {Competition} failed", comp.Key);
                results.Add(Skipped(comp, data!.Provider, "The football data provider did not answer. Cached data stays on the site."));
            }
        }
        return results;
    }

    private static SyncResult Skipped(CompetitionRef comp, string provider, string why) => new(comp.Key, provider, 0, 0, 0, 0, 0, [], why);

    private async Task<SyncResult> SyncOneAsync(IFootballData data, CompetitionRef comp, CancellationToken ct)
    {
        if (data.Provider == ApiFootball && !await quota.CanCallAsync(2, ct))
        {
            return Skipped(comp, data.Provider, "Today's API-FOOTBALL quota is used up; the sync waits until tomorrow.");
        }
        var season = await data.CurrentSeasonAsync(comp.ExternalLeagueId, ct);
        var seasonId = await repository.EnsureSeasonAsync(comp.Id, season, ct);
        var now = clock.UtcNow;
        var full = await repository.FullSyncDueAsync(seasonId, now - FullRefresh, ct);
        var rounds = full ? null : await repository.RoundsBetweenAsync(seasonId, now.AddDays(-3), now.AddDays(10), ct);
        var fixtures = rounds is { Count: 0 } ? [] : await data.FixturesAsync(comp.ExternalLeagueId, season, rounds, ct);
        var (clubs, players) = comp.FantasyEnabled
            ? await SyncSquadsAsync(data, comp, season, seasonId, ct)
            : (await repository.UpsertClubsAsync(seasonId, await data.ClubsAsync(comp.ExternalLeagueId, season, ct), ct), 0);
        var gameweeks = await repository.UpsertGameweeksAsync(seasonId, FootballSyncRules.Gameweeks(fixtures, now), ct);
        var saved = await repository.UpsertFixturesAsync(fixtures, gameweeks, clubs, ct);
        var (stats, scored) = comp.FantasyEnabled ? await SyncFantasyAsync(data, comp, seasonId, ct) : (0, []);
        await repository.MarkSyncedAsync(seasonId, now, full, ct);
        return new SyncResult(comp.Key, data.Provider, season, clubs.Count, players, saved, stats, scored, null);
    }

    /// <summary>Statistics of finished matches, then scoring of every gameweek that is over.</summary>
    private async Task<(int Stats, IReadOnlyList<int> Scored)> SyncFantasyAsync(IFootballData data, CompetitionRef comp, long seasonId, CancellationToken ct)
    {
        var stats = await SyncStatsAsync(data, comp, seasonId, ct);
        var scored = new List<int>();
        foreach (var gameweekId in await repository.GameweeksToScoreAsync(seasonId, ct))
        {
            scored.Add(await scoring.ScoreAsync(gameweekId, ct));
        }
        return (stats, scored);
    }

    /// <summary>Clubs and squads are read again once a week (one call per club), else the stored ones are used.</summary>
    private async Task<(IReadOnlyDictionary<int, long> Clubs, int Players)> SyncSquadsAsync(IFootballData data, CompetitionRef comp, int season, long seasonId, CancellationToken ct)
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
            if (data.Provider == ApiFootball && !await quota.CanCallAsync(1, ct))
            {
                break;
            }
            players += await repository.UpsertPlayersAsync(await data.SquadAsync(comp.ExternalLeagueId, season, club.ExternalId, ct), ids, ct);
        }
        return (ids, players);
    }

    /// <summary>Reads the statistics of every match that needs them, then saves them in one go.</summary>
    private async Task<int> SyncStatsAsync(IFootballData data, CompetitionRef comp, long seasonId, CancellationToken ct)
    {
        var read = new List<(FixtureToStat Fixture, IReadOnlyList<FdPlayerStat> Stats)>();
        var max = data.Provider == ApiFootball ? MaxStatsPerRun : int.MaxValue;
        foreach (var fixture in await repository.FixturesNeedingStatsAsync(seasonId, clock.UtcNow, max, ct))
        {
            if (data.Provider == ApiFootball && !await quota.CanCallAsync(1, ct))
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
