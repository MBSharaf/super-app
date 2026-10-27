using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Football;

/// <summary>A competition to sync, with its provider's league id and whether it has fantasy.</summary>
public sealed record CompetitionRef(int Id, string Key, int ExternalLeagueId, string NameEn, string Provider = "api-football", bool FantasyEnabled = true);

/// <summary>A gameweek as the fixtures describe it: deadline 90 minutes before its first kick-off.</summary>
public sealed record GameweekPlan(int Number, DateTime Deadline, GameweekStatus Status);

/// <summary>A finished match whose player statistics must be (re)read from the provider.</summary>
public sealed record FixtureToStat(long FixtureId, int ExternalId, long HomeClubId, long AwayClubId, int HomeGoals, int AwayGoals);

/// <summary>A player of the fantasy tables, found by the provider's id.</summary>
public sealed record KnownPlayer(long Id, long ClubId, PlayerPosition Position);

/// <summary>One statistics row ready to save, points already calculated.</summary>
public sealed record StatToSave(long PlayerId, int Minutes, int Goals, int Assists, int Saves, int OwnGoals, int PenaltiesSaved, int PenaltiesMissed,
    int YellowCards, bool RedCard, bool CleanSheet, int GoalsConceded, int Bonus, int Points);

/// <summary>What one sync did, per competition (shown to the admin who forced it).</summary>
public sealed record SyncResult(string Competition, string Provider, int Season, int Clubs, int Players, int Fixtures, int StatsRead, IReadOnlyList<int> GameweeksScored, string? Skipped);

/// <summary>Writes what the provider returns into the fantasy tables (insert or update by the provider's ids).</summary>
public interface IFantasySyncRepository
{
    Task<IReadOnlyList<CompetitionRef>> CompetitionsAsync(string? key, CancellationToken ct);

    /// <summary>The season row (created when new) and marks it as the competition's current season.</summary>
    Task<long> EnsureSeasonAsync(int competitionId, int year, CancellationToken ct);

    /// <summary>True when the season has no matches yet or its last full read is older than <paramref name="since"/>.</summary>
    Task<bool> FullSyncDueAsync(long seasonId, DateTime since, CancellationToken ct);

    /// <summary>The rounds with a match kicking off between <paramref name="from"/> and <paramref name="to"/> (what a quick sync reads).</summary>
    Task<IReadOnlyList<int>> RoundsBetweenAsync(long seasonId, DateTime from, DateTime to, CancellationToken ct);

    /// <summary>True when the season already has its clubs and players (squads are re-read once a week only).</summary>
    Task<bool> SquadsFreshAsync(long seasonId, DateTime since, CancellationToken ct);

    Task<IReadOnlyDictionary<int, long>> UpsertClubsAsync(long seasonId, IReadOnlyList<FdClub> clubs, CancellationToken ct);

    Task<IReadOnlyDictionary<int, long>> ClubIdsAsync(long seasonId, CancellationToken ct);

    Task<int> UpsertPlayersAsync(IReadOnlyList<FdPlayer> players, IReadOnlyDictionary<int, long> clubs, CancellationToken ct);

    /// <summary>Creates or moves gameweeks. A scored gameweek keeps "Finished" unless new statistics arrived.</summary>
    Task<IReadOnlyDictionary<int, long>> UpsertGameweeksAsync(long seasonId, IReadOnlyList<GameweekPlan> plans, CancellationToken ct);

    Task<int> UpsertFixturesAsync(IReadOnlyList<FdFixture> fixtures, IReadOnlyDictionary<int, long> gameweeks, IReadOnlyDictionary<int, long> clubs, CancellationToken ct);

    /// <summary>
    /// Finished matches without statistics, and matches whose statistics were read within a day of kick-off and are
    /// now older than that (one late re-check for the provider's corrections). At most <paramref name="max"/>.
    /// </summary>
    Task<IReadOnlyList<FixtureToStat>> FixturesNeedingStatsAsync(long seasonId, DateTime now, int max, CancellationToken ct);

    Task<IReadOnlyDictionary<int, KnownPlayer>> PlayersByExternalIdAsync(IReadOnlyCollection<int> externalIds, CancellationToken ct);

    /// <summary>Replaces the matches' statistics (one save); when they changed after scoring, the gameweek goes back to "Scoring".</summary>
    Task SaveStatsAsync(IReadOnlyList<(long FixtureId, IReadOnlyList<StatToSave> Rows)> batch, CancellationToken ct);

    /// <summary>Gameweeks whose matches are all over and that wait for (re)scoring.</summary>
    Task<IReadOnlyList<long>> GameweeksToScoreAsync(long seasonId, CancellationToken ct);

    Task MarkSyncedAsync(long seasonId, DateTime at, bool full, CancellationToken ct);
}

/// <summary>Pure rules of the sync: gameweeks from fixtures, statistics rows from a finished match.</summary>
public static class FootballSyncRules
{
    public static readonly TimeSpan DeadlineBeforeKickoff = TimeSpan.FromMinutes(90);

    public static IReadOnlyList<GameweekPlan> Gameweeks(IReadOnlyList<FdFixture> fixtures, DateTime now) =>
        fixtures.GroupBy(f => f.Round).OrderBy(g => g.Key).Select(g =>
        {
            var deadline = g.Min(f => f.KickoffAt) - DeadlineBeforeKickoff;
            var over = g.All(f => f.Status is FixtureStatus.Finished or FixtureStatus.Postponed or FixtureStatus.Cancelled)
                && g.Any(f => f.Status == FixtureStatus.Finished);
            var status = over ? GameweekStatus.Scoring : now >= deadline ? GameweekStatus.Live : GameweekStatus.Upcoming;
            return new GameweekPlan(g.Key, deadline, status);
        }).ToList();
}
