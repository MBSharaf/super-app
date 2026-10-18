using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>Public fantasy data (API §6.1): competitions, clubs, players, fixtures and the real league table — all from the cache.</summary>
public interface IFantasyService
{
    Task<IReadOnlyList<CompetitionDto>> CompetitionsAsync(CancellationToken ct);

    Task<BootstrapDto> BootstrapAsync(string competition, CancellationToken ct);

    Task<PagedResult<PlayerDto>> PlayersAsync(string competition, PlayersQuery query, CancellationToken ct);

    Task<PlayerDetailDto> PlayerAsync(string competition, long playerId, CancellationToken ct);

    Task<IReadOnlyList<FixtureDto>> FixturesAsync(string competition, int? gameweek, CancellationToken ct);

    Task<IReadOnlyList<TableRowDto>> TableAsync(string competition, CancellationToken ct);
}

public sealed class FantasyService(IFantasyReader reader, IFantasyTeamRepository teams, IClock clock) : IFantasyService
{
    public Task<IReadOnlyList<CompetitionDto>> CompetitionsAsync(CancellationToken ct) => reader.CompetitionsAsync(clock.UtcNow, ct);

    public async Task<BootstrapDto> BootstrapAsync(string competition, CancellationToken ct)
    {
        var season = await SeasonAsync(competition, ct);
        var info = await reader.CompetitionAsync(season.Key, clock.UtcNow, ct);
        return new BootstrapDto(info!, await reader.ClubsAsync(season.SeasonId, ct), await reader.PlayersAsync(season.SeasonId, ct),
            await reader.GameweeksAsync(season.SeasonId, ct));
    }

    public async Task<PagedResult<PlayerDto>> PlayersAsync(string competition, PlayersQuery query, CancellationToken ct)
    {
        var season = await SeasonAsync(competition, ct);
        IEnumerable<PlayerDto> players = await reader.PlayersAsync(season.SeasonId, ct);
        if (!string.IsNullOrWhiteSpace(query.Position))
        {
            players = players.Where(p => p.Position.Equals(query.Position, StringComparison.OrdinalIgnoreCase));
        }
        if (query.Club is { } club)
        {
            players = players.Where(p => p.ClubId == club);
        }
        if (query.MaxPrice is { } max)
        {
            players = players.Where(p => p.Price <= max);
        }
        if (!string.IsNullOrWhiteSpace(query.Q))
        {
            players = players.Where(p => p.Name.Contains(query.Q.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        return FantasyPaging.Page(Sort(players, query.Sort).ToList(), query.Page, query.PageSize);
    }

    private static IEnumerable<PlayerDto> Sort(IEnumerable<PlayerDto> players, string? sort) => sort?.ToLowerInvariant() switch
    {
        "price" => players.OrderByDescending(p => p.Price),
        "form" => players.OrderByDescending(p => p.Form),
        "selected" => players.OrderByDescending(p => p.SelectedBy),
        "goals" => players.OrderByDescending(p => p.Goals),
        "value" => players.OrderByDescending(p => p.TotalPoints / p.Price),
        _ => players.OrderByDescending(p => p.TotalPoints).ThenByDescending(p => p.Price),
    };

    public async Task<PlayerDetailDto> PlayerAsync(string competition, long playerId, CancellationToken ct)
    {
        var season = await SeasonAsync(competition, ct);
        var player = (await reader.PlayersAsync(season.SeasonId, ct)).FirstOrDefault(p => p.Id == playerId) ?? throw new NotFoundException("Player not found.");
        var history = await reader.PlayerHistoryAsync(playerId, season.SeasonId, ct);
        var upcoming = await reader.ClubFixturesAsync(season.SeasonId, player.ClubId, clock.UtcNow, 5, ct);
        return new PlayerDetailDto(player, history, upcoming);
    }

    public async Task<IReadOnlyList<FixtureDto>> FixturesAsync(string competition, int? gameweek, CancellationToken ct)
    {
        var season = await SeasonAsync(competition, ct);
        return await reader.FixturesAsync(season.SeasonId, gameweek, ct);
    }

    public async Task<IReadOnlyList<TableRowDto>> TableAsync(string competition, CancellationToken ct)
    {
        var season = await SeasonAsync(competition, ct);
        return LeagueTable.Build(await reader.ClubsAsync(season.SeasonId, ct), await reader.FixturesAsync(season.SeasonId, null, ct));
    }

    private async Task<SeasonInfo> SeasonAsync(string competition, CancellationToken ct) =>
        await teams.SeasonAsync(competition.ToUpperInvariant(), clock.UtcNow, ct)
        ?? throw new NotFoundException("This league has no season yet. Fantasy data arrives with the first sync.", "no_season");
}

/// <summary>The real league table from finished matches: 3 points a win, 1 a draw; ties by goal difference, then goals.</summary>
public static class LeagueTable
{
    public static IReadOnlyList<TableRowDto> Build(IReadOnlyList<ClubDto> clubs, IReadOnlyList<FixtureDto> fixtures)
    {
        var finished = fixtures.Where(f => f.Status == "Finished" && f.HomeGoals is not null && f.AwayGoals is not null).OrderBy(f => f.KickoffAt).ToList();
        var rows = clubs.Select(c =>
        {
            var games = finished.Where(f => f.HomeClubId == c.Id || f.AwayClubId == c.Id)
                .Select(f => f.HomeClubId == c.Id ? (For: f.HomeGoals!.Value, Against: f.AwayGoals!.Value) : (For: f.AwayGoals!.Value, Against: f.HomeGoals!.Value)).ToList();
            var (won, drawn) = (games.Count(g => g.For > g.Against), games.Count(g => g.For == g.Against));
            var form = games.TakeLast(5).Select(g => g.For > g.Against ? "W" : g.For == g.Against ? "D" : "L").ToList();
            return new TableRowDto(0, c.Id, c.Name, games.Count, won, drawn, games.Count - won - drawn, games.Sum(g => g.For), games.Sum(g => g.Against),
                won * 3 + drawn, form);
        }).OrderByDescending(r => r.Points).ThenByDescending(r => r.GoalsFor - r.GoalsAgainst).ThenByDescending(r => r.GoalsFor).ThenBy(r => r.Club).ToList();
        return rows.Select((r, i) => r with { Position = i + 1 }).ToList();
    }
}
