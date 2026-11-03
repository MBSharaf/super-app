using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Content;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Real matches from the fantasy cache (fixtures, clubs, player statistics).</summary>
public sealed class MatchesReader(AppDbContext db) : IMatchesReader
{
    public async Task<IReadOnlyList<MatchDto>> BetweenAsync(DateTime fromUtc, DateTime toUtc, string? competition, CancellationToken ct) =>
        await Project(db.Set<Fixture>().Where(f => f.Gameweek.Season.IsCurrent && f.KickoffAt >= fromUtc && f.KickoffAt < toUtc
                && (competition == null || f.Gameweek.Season.Competition.Key == competition))
            .OrderBy(f => f.KickoffAt).ThenBy(f => f.Id)).ToListAsync(ct);

    public async Task<MatchDetailDto?> DetailAsync(long id, CancellationToken ct)
    {
        var match = await Project(db.Set<Fixture>().Where(f => f.Id == id)).FirstOrDefaultAsync(ct);
        if (match is null)
        {
            return null;
        }
        var players = await db.Set<PlayerFixtureStat>().AsNoTracking().Where(s => s.FixtureId == id)
            .OrderBy(s => s.Player.ClubId).ThenBy(s => s.Player.Position).ThenBy(s => s.Player.Name)
            .Select(s => new { s.PlayerId, s.Player.ClubId, s.Player.Name, s.Player.Position, s.Minutes, s.Goals, s.Assists, s.YellowCards, s.RedCard, s.Points })
            .ToListAsync(ct);
        return new MatchDetailDto(match, players.Select(p => new MatchPlayerDto(p.PlayerId, p.ClubId, p.Name, FantasyReader.Code(p.Position), p.Minutes, p.Goals,
            p.Assists, p.YellowCards, p.RedCard, p.Points)).ToList());
    }

    private static IQueryable<MatchDto> Project(IQueryable<Fixture> query) =>
        query.AsNoTracking().Select(f => new MatchDto(f.Id, f.Gameweek.Season.Competition.Key, f.Gameweek.Number,
            new MatchClubDto(f.HomeClubId, f.HomeClub.NameEn, f.HomeClub.ShortName, f.HomeClub.KitColor, f.HomeClub.KitColor2),
            new MatchClubDto(f.AwayClubId, f.AwayClub.NameEn, f.AwayClub.ShortName, f.AwayClub.KitColor, f.AwayClub.KitColor2),
            f.KickoffAt, f.Status.ToString(), f.HomeGoals, f.AwayGoals, f.Minute, f.Venue));

    public async Task<IReadOnlyList<MatchCompetitionDto>> CompetitionsAsync(CancellationToken ct) =>
        await db.Set<Competition>().AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder)
            .Select(c => new MatchCompetitionDto(c.Key, c.NameEn, c.NameAr, c.ShortName, c.Region, c.Color, c.FantasyEnabled)).ToListAsync(ct);
}
