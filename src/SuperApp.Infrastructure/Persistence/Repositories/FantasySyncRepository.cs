using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Inserts or updates clubs, players, gameweeks, fixtures and statistics by the provider's ids.</summary>
public sealed class FantasySyncRepository(AppDbContext db, IClock clock) : IFantasySyncRepository
{
    private static readonly TimeSpan LateCheck = TimeSpan.FromHours(26);

    public async Task<IReadOnlyList<CompetitionRef>> CompetitionsAsync(string? key, CancellationToken ct) =>
        await db.Set<Competition>().AsNoTracking().Where(c => c.IsActive && (key == null || c.Key == key)).OrderBy(c => c.SortOrder)
            .Select(c => new CompetitionRef(c.Id, c.Key, c.ExternalLeagueId, c.NameEn, c.Provider, c.FantasyEnabled)).ToListAsync(ct);

    public async Task<long> EnsureSeasonAsync(int competitionId, int year, CancellationToken ct)
    {
        var seasons = await db.Set<Season>().Where(s => s.CompetitionId == competitionId).ToListAsync(ct);
        var season = seasons.FirstOrDefault(s => s.Year == year);
        if (season is { IsCurrent: true })
        {
            return season.Id;
        }
        seasons.Where(s => s.IsCurrent).ToList().ForEach(s => s.IsCurrent = false);
        await db.SaveChangesAsync(ct);
        if (season is null)
        {
            season = new Season { CompetitionId = competitionId, Year = (short)year };
            db.Set<Season>().Add(season);
        }
        season.IsCurrent = true;
        await db.SaveChangesAsync(ct);
        return season.Id;
    }

    public async Task<bool> FullSyncDueAsync(long seasonId, DateTime since, CancellationToken ct) =>
        !await db.Set<Season>().AnyAsync(s => s.Id == seasonId && s.FullSyncedAt >= since && s.Gameweeks.Any(g => g.Fixtures.Count > 0), ct);

    public async Task<IReadOnlyList<int>> RoundsBetweenAsync(long seasonId, DateTime from, DateTime to, CancellationToken ct) =>
        await db.Set<Fixture>().Where(f => f.Gameweek.SeasonId == seasonId && f.KickoffAt >= from && f.KickoffAt <= to)
            .Select(f => (int)f.Gameweek.Number).Distinct().ToListAsync(ct);

    public Task<bool> SquadsFreshAsync(long seasonId, DateTime since, CancellationToken ct) =>
        db.Set<SeasonClub>().AnyAsync(sc => sc.SeasonId == seasonId && (sc.Club.UpdatedAt ?? sc.Club.CreatedAt) >= since
            && db.Set<Player>().Any(p => p.ClubId == sc.ClubId), ct);

    public async Task<IReadOnlyDictionary<int, long>> UpsertClubsAsync(long seasonId, IReadOnlyList<FdClub> clubs, CancellationToken ct)
    {
        var ids = clubs.Select(c => c.ExternalId).ToList();
        var rows = await db.Set<Club>().Where(c => ids.Contains(c.ExternalId)).ToDictionaryAsync(c => c.ExternalId, ct);
        foreach (var c in clubs)
        {
            if (!rows.TryGetValue(c.ExternalId, out var row))
            {
                row = new Club { ExternalId = c.ExternalId, NameEn = c.Name, NameAr = c.Name, KitColor = c.KitColor, KitColor2 = c.KitColor2 };
                db.Set<Club>().Add(row);
                rows[c.ExternalId] = row;
            }
            row.NameEn = c.Name;
            row.ShortName = c.ShortName.Length > 5 ? c.ShortName[..5] : c.ShortName;
            row.UpdatedAt = clock.UtcNow;   // marks when the squads were last read
        }
        await db.SaveChangesAsync(ct);
        var linked = await db.Set<SeasonClub>().Where(sc => sc.SeasonId == seasonId).Select(sc => sc.ClubId).ToListAsync(ct);
        db.Set<SeasonClub>().AddRange(rows.Values.Where(r => !linked.Contains(r.Id)).Select(r => new SeasonClub { SeasonId = seasonId, ClubId = r.Id }));
        await db.SaveChangesAsync(ct);
        return rows.ToDictionary(r => r.Key, r => r.Value.Id);
    }

    public async Task<IReadOnlyDictionary<int, long>> ClubIdsAsync(long seasonId, CancellationToken ct) =>
        await db.Set<SeasonClub>().Where(sc => sc.SeasonId == seasonId).Select(sc => new { sc.Club.ExternalId, sc.ClubId })
            .ToDictionaryAsync(x => x.ExternalId, x => x.ClubId, ct);

    public async Task<int> UpsertPlayersAsync(IReadOnlyList<FdPlayer> players, IReadOnlyDictionary<int, long> clubs, CancellationToken ct)
    {
        var ids = players.Select(p => p.ExternalId).ToList();
        var rows = await db.Set<Player>().Where(p => ids.Contains(p.ExternalId)).ToDictionaryAsync(p => p.ExternalId, ct);
        foreach (var p in players.Where(p => clubs.ContainsKey(p.ClubExternalId)))
        {
            if (!rows.TryGetValue(p.ExternalId, out var row))
            {
                row = new Player { ExternalId = p.ExternalId, Price = Math.Clamp(p.Price, 3.5m, 15m), Position = p.Position };
                db.Set<Player>().Add(row);
            }
            row.ClubId = clubs[p.ClubExternalId];
            row.Name = p.Name.Length > 100 ? p.Name[..100] : p.Name;
            row.Status = p.Status;
        }
        return await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, long>> UpsertGameweeksAsync(long seasonId, IReadOnlyList<GameweekPlan> plans, CancellationToken ct)
    {
        var rows = await db.Set<Gameweek>().Where(g => g.SeasonId == seasonId).ToDictionaryAsync(g => (int)g.Number, ct);
        foreach (var plan in plans.Where(p => p.Number is >= 1 and <= 255))
        {
            if (!rows.TryGetValue(plan.Number, out var row))
            {
                row = new Gameweek { SeasonId = seasonId, Number = (byte)plan.Number };
                db.Set<Gameweek>().Add(row);
                rows[plan.Number] = row;
            }
            row.Deadline = plan.Deadline;
            var scored = row.Status == GameweekStatus.Finished && plan.Status == GameweekStatus.Scoring;
            row.Status = scored ? GameweekStatus.Finished : plan.Status;
        }
        await db.SaveChangesAsync(ct);
        return rows.ToDictionary(r => r.Key, r => r.Value.Id);
    }

    public async Task<int> UpsertFixturesAsync(IReadOnlyList<FdFixture> fixtures, IReadOnlyDictionary<int, long> gameweeks, IReadOnlyDictionary<int, long> clubs, CancellationToken ct)
    {
        var ids = fixtures.Select(f => f.ExternalId).ToList();
        var rows = await db.Set<Fixture>().Where(f => ids.Contains(f.ExternalId)).ToDictionaryAsync(f => f.ExternalId, ct);
        foreach (var f in fixtures.Where(f => gameweeks.ContainsKey(f.Round) && clubs.ContainsKey(f.HomeClubExternalId) && clubs.ContainsKey(f.AwayClubExternalId)))
        {
            if (!rows.TryGetValue(f.ExternalId, out var row))
            {
                row = new Fixture { ExternalId = f.ExternalId };
                db.Set<Fixture>().Add(row);
            }
            row.GameweekId = gameweeks[f.Round];
            (row.HomeClubId, row.AwayClubId) = (clubs[f.HomeClubExternalId], clubs[f.AwayClubExternalId]);
            row.KickoffAt = f.KickoffAt;
            row.Status = f.Status;
            row.HomeGoals = (byte?)f.HomeGoals;
            row.AwayGoals = (byte?)f.AwayGoals;
            row.Minute = (byte?)f.Minute;
            row.Venue = f.Venue is { Length: > 120 } v ? v[..120] : f.Venue;
        }
        await db.SaveChangesAsync(ct);
        return fixtures.Count;
    }

    public async Task<IReadOnlyList<FixtureToStat>> FixturesNeedingStatsAsync(long seasonId, DateTime now, int max, CancellationToken ct)
    {
        var lateBefore = now - LateCheck;
        return await db.Set<Fixture>().AsNoTracking()
            .Where(f => f.Gameweek.SeasonId == seasonId && f.Status == FixtureStatus.Finished)
            .Where(f => !f.PlayerStats.Any()
                || (f.KickoffAt < lateBefore && f.PlayerStats.All(s => (s.UpdatedAt ?? s.CreatedAt) < f.KickoffAt.AddHours(LateCheck.TotalHours))))
            .OrderBy(f => f.KickoffAt).Take(max)
            .Select(f => new FixtureToStat(f.Id, f.ExternalId, f.HomeClubId, f.AwayClubId, f.HomeGoals ?? 0, f.AwayGoals ?? 0))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, KnownPlayer>> PlayersByExternalIdAsync(IReadOnlyCollection<int> externalIds, CancellationToken ct) =>
        await db.Set<Player>().AsNoTracking().Where(p => externalIds.Contains(p.ExternalId))
            .ToDictionaryAsync(p => p.ExternalId, p => new KnownPlayer(p.Id, p.ClubId, p.Position), ct);

    public async Task SaveStatsAsync(IReadOnlyList<(long FixtureId, IReadOnlyList<StatToSave> Rows)> batch, CancellationToken ct)
    {
        var ids = batch.Select(b => b.FixtureId).ToList();
        var stored = (await db.Set<PlayerFixtureStat>().Where(s => ids.Contains(s.FixtureId)).ToListAsync(ct)).ToLookup(s => s.FixtureId);
        var changed = new List<long>();
        foreach (var (fixtureId, rows) in batch)
        {
            if (Apply(fixtureId, stored[fixtureId].ToDictionary(s => s.PlayerId), rows))
            {
                changed.Add(fixtureId);
            }
        }
        if (changed.Count > 0)
        {
            await db.Set<Gameweek>().Where(g => g.Fixtures.Any(f => changed.Contains(f.Id)) && g.Status == GameweekStatus.Finished)
                .ExecuteUpdateAsync(u => u.SetProperty(g => g.Status, GameweekStatus.Scoring), ct);
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Adds, updates and removes one match's rows; true when stored points changed (a correction).</summary>
    private bool Apply(long fixtureId, Dictionary<long, PlayerFixtureStat> existing, IReadOnlyList<StatToSave> rows)
    {
        var changed = existing.Count > 0 && (existing.Count != rows.Count || rows.Any(r => !existing.TryGetValue(r.PlayerId, out var e) || e.Points != r.Points));
        db.Set<PlayerFixtureStat>().RemoveRange(existing.Values.Where(e => rows.All(r => r.PlayerId != e.PlayerId)));
        foreach (var r in rows)
        {
            if (!existing.TryGetValue(r.PlayerId, out var row))
            {
                row = new PlayerFixtureStat { FixtureId = fixtureId, PlayerId = r.PlayerId };
                db.Set<PlayerFixtureStat>().Add(row);
            }
            Copy(r, row);
            row.UpdatedAt = existing.Count > 0 ? clock.UtcNow : null;
        }
        return changed;
    }

    private static void Copy(StatToSave r, PlayerFixtureStat row)
    {
        row.Minutes = (byte)Math.Min(r.Minutes, 130);
        (row.Goals, row.Assists, row.Saves, row.OwnGoals) = ((byte)r.Goals, (byte)r.Assists, (byte)r.Saves, (byte)r.OwnGoals);
        (row.PenaltiesSaved, row.PenaltiesMissed) = ((byte)r.PenaltiesSaved, (byte)r.PenaltiesMissed);
        row.YellowCards = (byte)Math.Min(r.YellowCards, 2);
        row.RedCard = r.RedCard;
        row.CleanSheet = r.CleanSheet;
        row.GoalsConceded = (byte)r.GoalsConceded;
        row.Bonus = (byte)Math.Min(r.Bonus, 3);
        row.Points = (short)r.Points;
    }

    public async Task<IReadOnlyList<long>> GameweeksToScoreAsync(long seasonId, CancellationToken ct) =>
        await db.Set<Gameweek>().Where(g => g.SeasonId == seasonId && g.Status == GameweekStatus.Scoring)
            .Where(g => g.Fixtures.Where(f => f.Status == FixtureStatus.Finished).All(f => db.Set<PlayerFixtureStat>().Any(s => s.FixtureId == f.Id)))
            .OrderBy(g => g.Number).Select(g => g.Id).ToListAsync(ct);

    public Task MarkSyncedAsync(long seasonId, DateTime at, bool full, CancellationToken ct) =>
        db.Set<Season>().Where(s => s.Id == seasonId).ExecuteUpdateAsync(u => u
            .SetProperty(s => s.LastSyncedAt, at)
            .SetProperty(s => s.FullSyncedAt, s => full ? at : s.FullSyncedAt), ct);
}
