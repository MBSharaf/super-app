namespace SuperApp.Application.Features.Content;

/* ---------- League tables and match facts (from the live feed) ---------- */

/// <summary>A competition's table: one group for a league, several for a cup's groups. <c>Color</c> marks a qualification place.</summary>
public sealed record StandingsDto(string Competition, string? Season, IReadOnlyList<StandingsGroupDto> Groups, IReadOnlyList<StandingsLegendDto> Legend);

public sealed record StandingsGroupDto(string? Name, IReadOnlyList<StandingRowDto> Rows);

public sealed record StandingRowDto(int Position, string Club, string? Logo, int Played, int Won, int Drawn, int Lost, int GoalsFor, int GoalsAgainst,
    int GoalDifference, int Points, string? Color, string? Form = null);

public sealed record StandingsLegendDto(string Title, string Color);

/// <summary>What happened in a match: statistics by group, events (goals, cards, substitutions) and the line-ups.</summary>
public sealed record MatchFactsDto(IReadOnlyList<MatchStatGroupDto> Stats, IReadOnlyList<MatchEventDto> Events, MatchLineupDto? Home, MatchLineupDto? Away);

public sealed record MatchStatGroupDto(string Title, IReadOnlyList<MatchStatDto> Items);

/// <summary>One statistic; <c>Home</c> / <c>Away</c> as shown ("57", "1.04", "245 (83%)"). <c>Bar</c>: draw it as a split bar.</summary>
public sealed record MatchStatDto(string Key, string Title, string Home, string Away, bool Bar);

/// <summary>Type goal, card or sub; Side home or away; Detail Yellow / Red / Penalty / Own goal.</summary>
public sealed record MatchEventDto(int Minute, int? Added, string Type, string Side, string? Player, string? Detail, string? PlayerIn, string? PlayerOut,
    int? HomeScore, int? AwayScore);

public sealed record MatchLineupDto(string? Formation, string? Coach, IReadOnlyList<LineupPlayerDto> Starters, IReadOnlyList<LineupPlayerDto> Subs);

public sealed record LineupPlayerDto(string Name, string? Number, string? Rating);

/// <summary>Tables and match facts from the live feed. Null when the feed has nothing for it (the site then says so).</summary>
public interface IMatchInsights
{
    Task<StandingsDto?> StandingsAsync(string competition, CancellationToken ct);

    Task<MatchFactsDto?> FactsAsync(long matchId, CancellationToken ct);
}
