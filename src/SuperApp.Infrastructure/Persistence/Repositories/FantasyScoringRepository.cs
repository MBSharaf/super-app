using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Fantasy;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Reads and writes of the scoring job: picks, player totals, team rows, ranks and group tables.</summary>
public sealed class FantasyScoringRepository(AppDbContext db, IClock clock) : IFantasyScoringRepository
{
    public Task<GameweekToScore?> GameweekAsync(long gameweekId, CancellationToken ct) =>
        db.Set<Gameweek>().AsNoTracking().Where(g => g.Id == gameweekId)
            .Select(g => new GameweekToScore(g.Id, g.SeasonId, g.Number, g.Season.Competition.Key, g.ScoredAt,
                g.Fixtures.Any() && g.Fixtures.All(f => f.Status == FixtureStatus.Finished || f.Status == FixtureStatus.Postponed || f.Status == FixtureStatus.Cancelled)))
            .FirstOrDefaultAsync(ct);

    public Task<long?> GameweekIdAsync(string competitionKey, int number, CancellationToken ct) =>
        db.Set<Gameweek>().Where(g => g.Season.IsCurrent && g.Season.Competition.Key == competitionKey && g.Number == number)
            .Select(g => (long?)g.Id).FirstOrDefaultAsync(ct);

    public async Task CarryPicksForwardAsync(GameweekToScore gameweek, CancellationToken ct)
    {
        var missing = await db.Set<FantasyTeam>().Where(t => t.SeasonId == gameweek.SeasonId && !t.Picks.Any(p => p.GameweekId == gameweek.Id))
            .Select(t => t.Id).ToListAsync(ct);
        if (missing.Count == 0)
        {
            return;
        }
        var earlier = await db.Set<Pick>().AsNoTracking()
            .Where(p => missing.Contains(p.TeamId) && p.Gameweek.Number < gameweek.Number)
            .Select(p => new { p.TeamId, p.Gameweek.Number, p.PlayerId, p.Slot, p.IsCaptain, p.IsViceCaptain }).ToListAsync(ct);
        foreach (var team in earlier.GroupBy(p => p.TeamId))
        {
            var latest = team.Max(p => p.Number);
            db.Set<Pick>().AddRange(team.Where(p => p.Number == latest).Select(p => new Pick
            {
                TeamId = p.TeamId, GameweekId = gameweek.Id, PlayerId = p.PlayerId, Slot = p.Slot, IsCaptain = p.IsCaptain, IsViceCaptain = p.IsViceCaptain,
            }));
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<long, PlayerGameweek>> PlayerTotalsAsync(long gameweekId, CancellationToken ct) =>
        await db.Set<PlayerFixtureStat>().Where(s => s.Fixture.GameweekId == gameweekId)
            .GroupBy(s => s.PlayerId)
            .Select(g => new { g.Key, Minutes = g.Sum(s => (int)s.Minutes), Points = g.Sum(s => (int)s.Points) })
            .ToDictionaryAsync(x => x.Key, x => new PlayerGameweek(x.Minutes, x.Points), ct);

    public async Task<IReadOnlyList<TeamToScore>> TeamsAsync(long gameweekId, CancellationToken ct)
    {
        var picks = await db.Set<Pick>().AsNoTracking().Where(p => p.GameweekId == gameweekId)
            .Select(p => new { p.TeamId, p.Team.UserId, p.PlayerId, p.Slot, p.Player.Position, p.IsCaptain, p.IsViceCaptain }).ToListAsync(ct);
        var rows = await db.Set<TeamGameweekPoint>().AsNoTracking().Where(r => r.GameweekId == gameweekId)
            .ToDictionaryAsync(r => r.TeamId, r => (r.ChipPlayed, (int)r.TransfersCost), ct);
        return picks.GroupBy(p => (p.TeamId, p.UserId)).OrderBy(g => g.Key.TeamId).Select(g =>
        {
            var (chip, cost) = rows.GetValueOrDefault(g.Key.TeamId);
            var scoring = g.Select(p => new ScoringPick(p.PlayerId, p.Slot, p.Position, p.IsCaptain, p.IsViceCaptain)).ToList();
            return new TeamToScore(g.Key.TeamId, g.Key.UserId, scoring, chip, cost);
        }).ToList();
    }

    public async Task SaveAsync(GameweekToScore gameweek, IReadOnlyList<TeamScored> scores, CancellationToken ct)
    {
        if (await db.Set<FantasyTeam>().AnyAsync(t => t.SeasonId == gameweek.SeasonId, ct))
        {
            await SavePicksAsync(gameweek.Id, scores, ct);
            await SaveTeamRowsAsync(gameweek.Id, scores, ct);
            await UpdateTeamsAsync(gameweek, ct);
            await RankGroupsAsync(gameweek.SeasonId, ct);
            await SelectedByAsync(gameweek, ct);
        }
        await db.Set<Gameweek>().Where(g => g.Id == gameweek.Id).ExecuteUpdateAsync(u => u
            .SetProperty(g => g.Status, GameweekStatus.Finished).SetProperty(g => g.ScoredAt, clock.UtcNow), ct);
    }

    private async Task SavePicksAsync(long gameweekId, IReadOnlyList<TeamScored> scores, CancellationToken ct)
    {
        var byTeam = scores.ToDictionary(s => s.TeamId);
        foreach (var pick in await db.Set<Pick>().Where(p => p.GameweekId == gameweekId).ToListAsync(ct))
        {
            var scored = byTeam.TryGetValue(pick.TeamId, out var team) ? team.Score.Picks.FirstOrDefault(p => p.PlayerId == pick.PlayerId) : null;
            pick.Multiplier = (byte)(scored?.Multiplier ?? 0);
            pick.Points = (short)(scored?.BasePoints ?? 0);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SaveTeamRowsAsync(long gameweekId, IReadOnlyList<TeamScored> scores, CancellationToken ct)
    {
        var rows = await db.Set<TeamGameweekPoint>().Where(r => r.GameweekId == gameweekId).ToDictionaryAsync(r => r.TeamId, ct);
        foreach (var score in scores)
        {
            if (!rows.TryGetValue(score.TeamId, out var row))
            {
                row = new TeamGameweekPoint { TeamId = score.TeamId, GameweekId = gameweekId };
                db.Set<TeamGameweekPoint>().Add(row);
                rows[score.TeamId] = row;
            }
            row.Points = (short)score.Net;
        }
        Rank(rows.Values.ToList(), r => r.Points, (r, rank) => r.Rank = rank);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Totals and overall ranks for the season; one more free transfer (up to 2) after the first scoring.</summary>
    private async Task UpdateTeamsAsync(GameweekToScore gameweek, CancellationToken ct)
    {
        var teams = await db.Set<FantasyTeam>().Where(t => t.SeasonId == gameweek.SeasonId).ToListAsync(ct);
        // Rows of gameweeks not scored yet hold 0 points (they only carry a chip or the transfer cost), so all rows add up
        var totals = await db.Set<TeamGameweekPoint>().Where(r => r.Team.SeasonId == gameweek.SeasonId)
            .GroupBy(r => r.TeamId).Select(g => new { g.Key, Total = g.Sum(r => (int)r.Points) }).ToDictionaryAsync(x => x.Key, x => x.Total, ct);
        foreach (var team in teams)
        {
            team.TotalPoints = totals.GetValueOrDefault(team.Id);
            if (gameweek.ScoredAt is null)
            {
                team.FreeTransfers = (byte)Math.Min(SquadRules.MaxSavedFreeTransfers, team.FreeTransfers + 1);
            }
        }
        Rank(teams, t => t.TotalPoints, (t, rank) => t.OverallRank = rank);
        await db.SaveChangesAsync(ct);
    }

    private async Task RankGroupsAsync(long seasonId, CancellationToken ct)
    {
        var members = await db.Set<GroupMember>().Where(m => m.Group.SeasonId == seasonId).Include(m => m.Team).ToListAsync(ct);
        foreach (var group in members.GroupBy(m => m.GroupId))
        {
            Rank(group.ToList(), m => m.Team.TotalPoints, (m, rank) => m.Rank = rank);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SelectedByAsync(GameweekToScore gameweek, CancellationToken ct)
    {
        var teams = await db.Set<FantasyTeam>().CountAsync(t => t.SeasonId == gameweek.SeasonId, ct);
        if (teams == 0)
        {
            return;
        }
        var counts = await db.Set<Pick>().Where(p => p.GameweekId == gameweek.Id).GroupBy(p => p.PlayerId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var players = await db.Set<Player>().Where(p => db.Set<SeasonClub>().Any(sc => sc.SeasonId == gameweek.SeasonId && sc.ClubId == p.ClubId)).ToListAsync(ct);
        players.ForEach(p => p.SelectedByPercent = Math.Round(counts.GetValueOrDefault(p.Id) * 100m / teams, 2));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Standard competition ranking: equal scores share a rank (1, 2, 2, 4).</summary>
    private static void Rank<T>(List<T> rows, Func<T, int> score, Action<T, int> set)
    {
        var ordered = rows.OrderByDescending(score).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            set(ordered[i], i > 0 && score(ordered[i]) == score(ordered[i - 1]) ? RankOf(ordered, i, score) : i + 1);
        }
    }

    private static int RankOf<T>(List<T> ordered, int i, Func<T, int> score)
    {
        var first = i;
        while (first > 0 && score(ordered[first - 1]) == score(ordered[i]))
        {
            first--;
        }
        return first + 1;
    }

    public async Task QueueEventAsync(string type, object payload, CancellationToken ct)
    {
        db.Set<OutboxEvent>().Add(new OutboxEvent { Type = type, PayloadJson = JsonSerializer.Serialize(payload), OccurredAt = clock.UtcNow });
        await db.SaveChangesAsync(ct);
    }
}
