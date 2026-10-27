using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Football;

/* ---------- What a football data provider returns (FPL, TheSportsDB, API-FOOTBALL, or the simulated one) ---------- */

public sealed record FdClub(int ExternalId, string Name, string ShortName, string KitColor, string KitColor2);

public sealed record FdPlayer(int ExternalId, int ClubExternalId, string Name, PlayerPosition Position, decimal Price, PlayerStatus Status);

/// <summary>
/// A match. <c>Round</c> is the gameweek number ("Regular Season - 9" → 9); in cups TheSportsDB's codes are kept
/// (32 playoff, 16 last 16, 125 quarter-final, 150 semi-final, 200 final).
/// </summary>
public sealed record FdFixture(int ExternalId, int Round, int HomeClubExternalId, int AwayClubExternalId, DateTime KickoffAt,
    FixtureStatus Status, int? HomeGoals, int? AwayGoals, int? Minute, string? Venue = null);

/// <summary>A player's numbers in a finished match. <c>Rating</c> decides the bonus points (best three ratings).</summary>
public sealed record FdPlayerStat(int PlayerExternalId, int ClubExternalId, int Minutes, int Goals, int Assists, int Saves, int OwnGoals,
    int PenaltiesSaved, int PenaltiesMissed, int YellowCards, bool RedCard, decimal Rating);

/// <summary>
/// Football data (INTEGRATIONS §7): leagues, clubs, squads, fixtures and per-player match statistics. The site never
/// calls the provider; only the sync job does, and everything is cached in the fantasy tables. Squads and statistics
/// are read only for competitions with fantasy on.
/// </summary>
public interface IFootballData
{
    /// <summary>"fpl", "thesportsdb", "api-football" or "simulated".</summary>
    string Provider { get; }

    Task<int> CurrentSeasonAsync(int leagueId, CancellationToken ct);

    Task<IReadOnlyList<FdClub>> ClubsAsync(int leagueId, int season, CancellationToken ct);

    Task<IReadOnlyList<FdPlayer>> SquadAsync(int leagueId, int season, int clubExternalId, CancellationToken ct);

    /// <summary>
    /// The season's matches; <paramref name="rounds"/> limits the read to those rounds. Null = all of them, or for a
    /// provider read round by round (TheSportsDB) the rounds around the next match.
    /// </summary>
    Task<IReadOnlyList<FdFixture>> FixturesAsync(int leagueId, int season, IReadOnlyCollection<int>? rounds, CancellationToken ct);

    Task<IReadOnlyList<FdPlayerStat>> FixtureStatsAsync(int leagueId, int fixtureExternalId, CancellationToken ct);
}

/// <summary>
/// The provider of each competition (its <c>Provider</c> column): FPL, TheSportsDB, or API-FOOTBALL when its key is set.
/// Offline (setting <c>Football:Offline</c>, used by the tests) the simulated provider answers for the leagues it knows.
/// Null when the competition cannot be read now.
/// </summary>
public interface IFootballSources
{
    IFootballData? For(CompetitionRef competition);
}

/// <summary>Today's calls against the provider's daily quota (the CEO usage bar, FR-FAN-10).</summary>
public sealed record FootballUsage(string Provider, int Used, int Limit, int Percent, string Level, string? Message);

/// <summary>The daily quota of API-FOOTBALL (ops.IntegrationUsage). At 100% the jobs stop until the next day.</summary>
public interface IFootballQuota
{
    Task<FootballUsage> TodayAsync(CancellationToken ct);

    /// <summary>True while today's calls stay under the limit (the simulated provider is never limited).</summary>
    Task<bool> CanCallAsync(int calls, CancellationToken ct);
}
