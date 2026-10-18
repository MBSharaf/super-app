using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>The gameweek open for changes (its deadline is still ahead).</summary>
public sealed record OpenGameweek(long Id, int Number, DateTime Deadline);

/// <summary>A competition's current season and its rules.</summary>
public sealed record SeasonInfo(long SeasonId, int CompetitionId, string Key, int Year, decimal Budget, int MaxPerClub, OpenGameweek? Open);

/// <summary>Public fantasy data from the cache (never live from the provider).</summary>
public interface IFantasyReader
{
    Task<IReadOnlyList<CompetitionDto>> CompetitionsAsync(DateTime now, CancellationToken ct);

    Task<CompetitionDto?> CompetitionAsync(string key, DateTime now, CancellationToken ct);

    Task<IReadOnlyList<ClubDto>> ClubsAsync(long seasonId, CancellationToken ct);

    Task<IReadOnlyList<PlayerDto>> PlayersAsync(long seasonId, CancellationToken ct);

    Task<IReadOnlyList<GameweekDto>> GameweeksAsync(long seasonId, CancellationToken ct);

    Task<IReadOnlyList<FixtureDto>> FixturesAsync(long seasonId, int? gameweek, CancellationToken ct);

    Task<IReadOnlyList<PlayerGameweekDto>> PlayerHistoryAsync(long playerId, long seasonId, CancellationToken ct);

    Task<IReadOnlyList<FixtureDto>> ClubFixturesAsync(long seasonId, long clubId, DateTime from, int take, CancellationToken ct);
}

/// <summary>Squads, picks, transfers and chips.</summary>
public interface IFantasyTeamRepository
{
    Task<SeasonInfo?> SeasonAsync(string competitionKey, DateTime now, CancellationToken ct);

    Task<FantasyTeam?> TeamAsync(Guid userId, long seasonId, CancellationToken ct);

    /// <summary>Players of the season's clubs, by id (players of other competitions are left out).</summary>
    Task<IReadOnlyDictionary<long, SquadPlayer>> PlayersAsync(long seasonId, IReadOnlyCollection<long> ids, CancellationToken ct);

    /// <summary>The team's picks for the gameweek; when it has none yet, its latest earlier picks are copied forward.</summary>
    Task<List<Pick>> PicksAsync(long teamId, long gameweekId, CancellationToken ct);

    /// <summary>The team's row for the gameweek (chip played, transfer cost), created when missing.</summary>
    Task<TeamGameweekPoint> GameweekRowAsync(long teamId, long gameweekId, CancellationToken ct);

    /// <summary>
    /// Replaces the team's picks for the gameweek (players move between slots, which the unique slot and player indexes
    /// do not allow row by row). Saves everything else that is pending too.
    /// </summary>
    Task<List<Pick>> ReplacePicksAsync(long teamId, long gameweekId, IReadOnlyList<Pick> picks, CancellationToken ct);

    /// <summary>The chip played and the transfer cost so far for the gameweek (read only).</summary>
    Task<(string? Chip, int Cost)> GameweekStateAsync(long teamId, long gameweekId, CancellationToken ct);

    /// <summary>Saves a new team with its first picks. A second team for the same season → ConflictException (409).</summary>
    Task AddTeamAsync(FantasyTeam team, IReadOnlyList<Pick> picks, CancellationToken ct);

    void AddTransfers(IEnumerable<Transfer> transfers);

    Task<IReadOnlyList<TeamGameweekDto>> HistoryAsync(long teamId, CancellationToken ct);

    Task<int> ManagersAsync(long seasonId, CancellationToken ct);
}

/// <summary>A team going into the scoring of one gameweek.</summary>
public sealed record TeamToScore(long TeamId, Guid UserId, IReadOnlyList<ScoringPick> Picks, string? Chip, int TransfersCost);

public sealed record TeamScored(long TeamId, TeamScore Score, int Net);

/// <summary>The gameweek being scored. <c>Over</c>: every match is finished, postponed or cancelled.</summary>
public sealed record GameweekToScore(long Id, long SeasonId, int Number, string Competition, DateTime? ScoredAt, bool Over);

/// <summary>Reads and writes for the scoring job.</summary>
public interface IFantasyScoringRepository
{
    Task<GameweekToScore?> GameweekAsync(long gameweekId, CancellationToken ct);

    Task<long?> GameweekIdAsync(string competitionKey, int number, CancellationToken ct);

    /// <summary>Teams of the season without picks for this gameweek get their latest earlier picks.</summary>
    Task CarryPicksForwardAsync(GameweekToScore gameweek, CancellationToken ct);

    Task<IReadOnlyDictionary<long, PlayerGameweek>> PlayerTotalsAsync(long gameweekId, CancellationToken ct);

    Task<IReadOnlyList<TeamToScore>> TeamsAsync(long gameweekId, CancellationToken ct);

    /// <summary>
    /// Saves pick points and multipliers and the team rows; then totals, overall and gameweek ranks, group ranks,
    /// "selected by" percentages; on the first scoring one more free transfer (up to 2); the gameweek is Finished.
    /// </summary>
    Task SaveAsync(GameweekToScore gameweek, IReadOnlyList<TeamScored> scores, CancellationToken ct);

    /// <summary>Queues the <c>fantasy.gameweek.finished</c> event for n8n (ops.OutboxEvents).</summary>
    Task QueueEventAsync(string type, object payload, CancellationToken ct);
}

/// <summary>Scores one gameweek (players, teams, ranks, groups) and tells every manager their points.</summary>
public interface IFantasyScoringService
{
    /// <summary>Returns the gameweek number.</summary>
    Task<int> ScoreAsync(long gameweekId, CancellationToken ct);
}

/// <summary>Paging of the players list.</summary>
public static class FantasyPaging
{
    public static PagedResult<T> Page<T>(IReadOnlyList<T> all, int page, int pageSize)
    {
        var request = new PageRequest(page, pageSize);
        return new PagedResult<T>(all.Skip(request.Skip).Take(request.SafePageSize).ToList(), request.SafePage, request.SafePageSize, all.Count);
    }
}
