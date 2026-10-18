using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Fantasy;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Public fantasy data read from the cached tables.</summary>
public sealed class FantasyReader(AppDbContext db) : IFantasyReader
{
    private static readonly string[] Codes = ["GK", "DEF", "MID", "FWD"];

    public static string Code(PlayerPosition position) => Codes[(int)position];

    public async Task<IReadOnlyList<CompetitionDto>> CompetitionsAsync(DateTime now, CancellationToken ct)
    {
        var keys = await db.Set<Competition>().Where(c => c.IsActive).OrderBy(c => c.SortOrder).Select(c => c.Key).ToListAsync(ct);
        var list = new List<CompetitionDto>();
        foreach (var key in keys)
        {
            list.Add((await CompetitionAsync(key, now, ct))!);
        }
        return list;
    }

    public async Task<CompetitionDto?> CompetitionAsync(string key, DateTime now, CancellationToken ct)
    {
        var c = await db.Set<Competition>().AsNoTracking().FirstOrDefaultAsync(x => x.Key == key && x.IsActive, ct);
        if (c is null)
        {
            return null;
        }
        var season = await db.Set<Season>().AsNoTracking().Where(s => s.CompetitionId == c.Id && s.IsCurrent)
            .Select(s => new
            {
                s.Year, s.LastSyncedAt, Total = s.Gameweeks.Count(),
                Open = s.Gameweeks.Where(g => g.Deadline > now).OrderBy(g => g.Number).Select(g => new { g.Number, g.Deadline }).FirstOrDefault(),
                Finished = s.Gameweeks.Where(g => g.Status == GameweekStatus.Finished).Max(g => (int?)g.Number),
            }).FirstOrDefaultAsync(ct);
        return new CompetitionDto(c.Key, c.NameEn, c.NameAr, c.Country, season?.Year, season?.Open?.Number, season?.Open?.Deadline, season?.Finished,
            season?.Total ?? 0, c.SquadBudget, c.MaxPerClub, season?.LastSyncedAt);
    }

    public async Task<IReadOnlyList<ClubDto>> ClubsAsync(long seasonId, CancellationToken ct) =>
        await db.Set<SeasonClub>().AsNoTracking().Where(sc => sc.SeasonId == seasonId).OrderBy(sc => sc.Club.NameEn)
            .Select(sc => new ClubDto(sc.ClubId, sc.Club.NameEn, sc.Club.NameAr, sc.Club.ShortName, sc.Club.KitColor, sc.Club.KitColor2, null))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PlayerDto>> PlayersAsync(long seasonId, CancellationToken ct)
    {
        var players = await db.Set<Player>().AsNoTracking().Where(p => db.Set<SeasonClub>().Any(sc => sc.SeasonId == seasonId && sc.ClubId == p.ClubId))
            .Select(p => new { p.Id, p.Name, p.NameAr, p.ClubId, Club = p.Club.ShortName, p.Position, p.Price, p.Status, p.SelectedByPercent }).ToListAsync(ct);
        var totals = await db.Set<PlayerFixtureStat>().AsNoTracking().Where(s => s.Fixture.Gameweek.SeasonId == seasonId).GroupBy(s => s.PlayerId)
            .Select(g => new
            {
                g.Key, Points = g.Sum(s => (int)s.Points), Goals = g.Sum(s => (int)s.Goals), Assists = g.Sum(s => (int)s.Assists),
                CleanSheets = g.Count(s => s.CleanSheet), Minutes = g.Sum(s => (int)s.Minutes), Bonus = g.Sum(s => (int)s.Bonus),
                Yellow = g.Sum(s => (int)s.YellowCards), Saves = g.Sum(s => (int)s.Saves),
            }).ToDictionaryAsync(x => x.Key, ct);
        var recent = await RecentPointsAsync(seasonId, ct);
        return players.Select(p =>
        {
            var t = totals.GetValueOrDefault(p.Id);
            var (form, last) = recent.GetValueOrDefault(p.Id);
            return new PlayerDto(p.Id, p.Name, p.NameAr, p.ClubId, p.Club, Code(p.Position), p.Price, p.Status.ToString(), p.SelectedByPercent,
                t?.Points ?? 0, form, last, t?.Goals ?? 0, t?.Assists ?? 0, t?.CleanSheets ?? 0, t?.Minutes ?? 0, t?.Bonus ?? 0, t?.Yellow ?? 0, t?.Saves ?? 0);
        }).ToList();
    }

    /// <summary>Form = average points of the last three finished gameweeks; Last = points of the last one.</summary>
    private async Task<Dictionary<long, (decimal Form, int Last)>> RecentPointsAsync(long seasonId, CancellationToken ct)
    {
        var numbers = await db.Set<Gameweek>().Where(g => g.SeasonId == seasonId && g.Status == GameweekStatus.Finished)
            .OrderByDescending(g => g.Number).Select(g => (int)g.Number).Take(3).ToListAsync(ct);
        if (numbers.Count == 0)
        {
            return [];
        }
        var rows = await db.Set<PlayerFixtureStat>().AsNoTracking().Where(s => s.Fixture.Gameweek.SeasonId == seasonId && numbers.Contains(s.Fixture.Gameweek.Number))
            .GroupBy(s => new { s.PlayerId, s.Fixture.Gameweek.Number }).Select(g => new { g.Key.PlayerId, g.Key.Number, Points = g.Sum(s => (int)s.Points) })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.PlayerId).ToDictionary(g => g.Key, g => (
            Math.Round(g.Sum(r => r.Points) / 3m, 1), g.Where(r => r.Number == numbers[0]).Sum(r => r.Points)));
    }

    public async Task<IReadOnlyList<GameweekDto>> GameweeksAsync(long seasonId, CancellationToken ct) =>
        await db.Set<Gameweek>().AsNoTracking().Where(g => g.SeasonId == seasonId).OrderBy(g => g.Number)
            .Select(g => new GameweekDto(g.Number, g.Deadline, g.Status.ToString(), g.ScoredAt)).ToListAsync(ct);

    public async Task<IReadOnlyList<FixtureDto>> FixturesAsync(long seasonId, int? gameweek, CancellationToken ct) =>
        await Fixtures(db.Set<Fixture>().Where(f => f.Gameweek.SeasonId == seasonId && (gameweek == null || f.Gameweek.Number == gameweek))
            .OrderBy(f => f.KickoffAt).ThenBy(f => f.Id)).ToListAsync(ct);

    public async Task<IReadOnlyList<FixtureDto>> ClubFixturesAsync(long seasonId, long clubId, DateTime from, int take, CancellationToken ct) =>
        await Fixtures(db.Set<Fixture>().Where(f => f.Gameweek.SeasonId == seasonId && (f.HomeClubId == clubId || f.AwayClubId == clubId) && f.KickoffAt >= from)
            .OrderBy(f => f.KickoffAt).Take(take)).ToListAsync(ct);

    public static IQueryable<FixtureDto> Fixtures(IQueryable<Fixture> query) =>
        query.AsNoTracking().Select(f => new FixtureDto(f.Id, f.Gameweek.Number, f.HomeClubId, f.AwayClubId, f.HomeClub.ShortName, f.AwayClub.ShortName,
            f.KickoffAt, f.Status.ToString(), f.HomeGoals, f.AwayGoals, f.Minute));

    public async Task<IReadOnlyList<PlayerGameweekDto>> PlayerHistoryAsync(long playerId, long seasonId, CancellationToken ct) =>
        await db.Set<PlayerFixtureStat>().AsNoTracking().Where(s => s.PlayerId == playerId && s.Fixture.Gameweek.SeasonId == seasonId)
            .GroupBy(s => s.Fixture.Gameweek.Number).OrderByDescending(g => g.Key)
            .Select(g => new PlayerGameweekDto(g.Key, g.Sum(s => (int)s.Minutes), g.Sum(s => (int)s.Goals), g.Sum(s => (int)s.Assists),
                g.Count(s => s.CleanSheet), g.Sum(s => (int)s.Saves), g.Sum(s => (int)s.Bonus), g.Sum(s => (int)s.YellowCards), g.Any(s => s.RedCard),
                g.Sum(s => (int)s.Points))).ToListAsync(ct);
}
