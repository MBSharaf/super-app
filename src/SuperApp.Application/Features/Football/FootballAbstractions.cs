using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Football;

/* ---------- What a football data provider returns (API-FOOTBALL, or the simulated provider) ---------- */

public sealed record FdClub(int ExternalId, string Name, string ShortName, string KitColor, string KitColor2);

public sealed record FdPlayer(int ExternalId, int ClubExternalId, string Name, PlayerPosition Position, decimal Price, PlayerStatus Status);

/// <summary>A match. <c>Round</c> is the gameweek number ("Regular Season - 9" → 9).</summary>
public sealed record FdFixture(int ExternalId, int Round, int HomeClubExternalId, int AwayClubExternalId, DateTime KickoffAt,
    FixtureStatus Status, int? HomeGoals, int? AwayGoals, int? Minute);

/// <summary>A player's numbers in a finished match. <c>Rating</c> decides the bonus points (best three ratings).</summary>
public sealed record FdPlayerStat(int PlayerExternalId, int ClubExternalId, int Minutes, int Goals, int Assists, int Saves, int OwnGoals,
    int PenaltiesSaved, int PenaltiesMissed, int YellowCards, bool RedCard, decimal Rating);

/// <summary>
/// Football data (INTEGRATIONS §7): leagues, clubs, squads, fixtures and per-player match statistics. The site never
/// calls the provider; only the sync job does, and everything is cached in the fantasy tables. Without an API key
/// the simulated provider answers, so development and tests run offline.
/// </summary>
public interface IFootballData
{
    /// <summary>"simulated" or "api-football".</summary>
    string Provider { get; }

    Task<int> CurrentSeasonAsync(int leagueId, CancellationToken ct);

    Task<IReadOnlyList<FdClub>> ClubsAsync(int leagueId, int season, CancellationToken ct);

    Task<IReadOnlyList<FdPlayer>> SquadAsync(int leagueId, int season, int clubExternalId, CancellationToken ct);

    Task<IReadOnlyList<FdFixture>> FixturesAsync(int leagueId, int season, CancellationToken ct);

    Task<IReadOnlyList<FdPlayerStat>> FixtureStatsAsync(int leagueId, int fixtureExternalId, CancellationToken ct);
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
