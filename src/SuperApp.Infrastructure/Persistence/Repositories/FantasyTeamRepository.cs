using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Fantasy;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Squads, picks per gameweek, transfers and the team's gameweek rows.</summary>
public sealed class FantasyTeamRepository(AppDbContext db) : IFantasyTeamRepository
{
    public Task<SeasonInfo?> SeasonAsync(string competitionKey, DateTime now, CancellationToken ct) =>
        db.Set<Season>().AsNoTracking().Where(s => s.IsCurrent && s.Competition.Key == competitionKey && s.Competition.IsActive && s.Competition.FantasyEnabled)
            .Select(s => new SeasonInfo(s.Id, s.CompetitionId, s.Competition.Key, s.Year, s.Competition.SquadBudget, s.Competition.MaxPerClub,
                s.Gameweeks.Where(g => g.Deadline > now).OrderBy(g => g.Number).Select(g => new OpenGameweek(g.Id, g.Number, g.Deadline)).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);

    public Task<FantasyTeam?> TeamAsync(Guid userId, long seasonId, CancellationToken ct) =>
        db.Set<FantasyTeam>().FirstOrDefaultAsync(t => t.UserId == userId && t.SeasonId == seasonId, ct);

    public async Task<IReadOnlyDictionary<long, SquadPlayer>> PlayersAsync(long seasonId, IReadOnlyCollection<long> ids, CancellationToken ct) =>
        await db.Set<Player>().AsNoTracking()
            .Where(p => ids.Contains(p.Id) && db.Set<SeasonClub>().Any(sc => sc.SeasonId == seasonId && sc.ClubId == p.ClubId))
            .ToDictionaryAsync(p => p.Id, p => new SquadPlayer(p.Id, p.ClubId, p.Position, p.Price), ct);

    public async Task<List<Pick>> PicksAsync(long teamId, long gameweekId, CancellationToken ct)
    {
        var picks = await db.Set<Pick>().Where(p => p.TeamId == teamId && p.GameweekId == gameweekId).OrderBy(p => p.Slot).ToListAsync(ct);
        if (picks.Count > 0)
        {
            return picks;
        }
        var number = await db.Set<Gameweek>().Where(g => g.Id == gameweekId).Select(g => g.Number).SingleAsync(ct);
        var latest = await db.Set<Pick>().AsNoTracking().Where(p => p.TeamId == teamId && p.Gameweek.Number < number)
            .GroupBy(p => p.GameweekId).OrderByDescending(g => g.Max(p => p.Gameweek.Number)).Select(g => g.Key).FirstOrDefaultAsync(ct);
        var copied = await db.Set<Pick>().AsNoTracking().Where(p => p.TeamId == teamId && p.GameweekId == latest).OrderBy(p => p.Slot).ToListAsync(ct);
        var fresh = copied.Select(p => new Pick
        {
            TeamId = teamId, GameweekId = gameweekId, PlayerId = p.PlayerId, Slot = p.Slot, IsCaptain = p.IsCaptain, IsViceCaptain = p.IsViceCaptain,
        }).ToList();
        db.Set<Pick>().AddRange(fresh);
        await db.SaveChangesAsync(ct);
        return fresh;
    }

    public async Task<TeamGameweekPoint> GameweekRowAsync(long teamId, long gameweekId, CancellationToken ct)
    {
        var row = await db.Set<TeamGameweekPoint>().FirstOrDefaultAsync(r => r.TeamId == teamId && r.GameweekId == gameweekId, ct);
        if (row is null)
        {
            row = new TeamGameweekPoint { TeamId = teamId, GameweekId = gameweekId };
            db.Set<TeamGameweekPoint>().Add(row);
        }
        return row;
    }

    public async Task<List<Pick>> ReplacePicksAsync(long teamId, long gameweekId, IReadOnlyList<Pick> picks, CancellationToken ct)
    {
        // One save (one transaction): EF deletes the old rows before inserting rows with the same unique slot / player
        var old = await db.Set<Pick>().Where(p => p.TeamId == teamId && p.GameweekId == gameweekId).ToListAsync(ct);
        db.Set<Pick>().RemoveRange(old);
        db.Set<Pick>().AddRange(picks);
        await db.SaveChangesAsync(ct);
        return [.. picks];
    }

    public async Task<(string? Chip, int Cost)> GameweekStateAsync(long teamId, long gameweekId, CancellationToken ct)
    {
        var row = await db.Set<TeamGameweekPoint>().AsNoTracking().Where(r => r.TeamId == teamId && r.GameweekId == gameweekId)
            .Select(r => new { r.ChipPlayed, r.TransfersCost }).FirstOrDefaultAsync(ct);
        return (row?.ChipPlayed, row?.TransfersCost ?? 0);
    }

    public async Task AddTeamAsync(FantasyTeam team, IReadOnlyList<Pick> picks, CancellationToken ct)
    {
        db.Set<FantasyTeam>().Add(team);
        foreach (var pick in picks)
        {
            pick.Team = team;
            db.Set<Pick>().Add(pick);
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            db.ChangeTracker.Clear();
            throw new ConflictException("You already have a team in this league.", "team_exists");
        }
    }

    public void AddTransfers(IEnumerable<Transfer> transfers) => db.Set<Transfer>().AddRange(transfers);

    public async Task<IReadOnlyList<TeamGameweekDto>> HistoryAsync(long teamId, CancellationToken ct)
    {
        var rows = await db.Set<TeamGameweekPoint>().AsNoTracking().Where(r => r.TeamId == teamId && r.Gameweek.ScoredAt != null)
            .OrderBy(r => r.Gameweek.Number).Select(r => new { r.GameweekId, r.Gameweek.Number, r.Points, r.TransfersCost, r.Rank, r.ChipPlayed }).ToListAsync(ct);
        var ids = rows.Select(r => r.GameweekId).ToList();
        var picks = (await db.Set<Pick>().AsNoTracking().Where(p => p.TeamId == teamId && ids.Contains(p.GameweekId)).OrderBy(p => p.Slot)
            .Select(p => new { p.GameweekId, Pick = new PickDto(p.PlayerId, p.Slot, p.IsCaptain, p.IsViceCaptain, p.Multiplier, p.Points) }).ToListAsync(ct))
            .ToLookup(p => p.GameweekId, p => p.Pick);
        var total = 0;
        return rows.Select(r =>
        {
            total += r.Points;
            return new TeamGameweekDto(r.Number, r.Points, r.TransfersCost, r.Rank, r.ChipPlayed, total, picks[r.GameweekId].ToList());
        }).ToList();
    }

    public Task<int> ManagersAsync(long seasonId, CancellationToken ct) => db.Set<FantasyTeam>().CountAsync(t => t.SeasonId == seasonId, ct);
}
