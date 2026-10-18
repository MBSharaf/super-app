using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
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
            f.KickoffAt, f.Status.ToString(), f.HomeGoals, f.AwayGoals, f.Minute));
}

/// <summary>Super App news posts (soft-deleted; hidden or future posts stay out of the public list).</summary>
public sealed class NewsRepository(AppDbContext db) : INewsRepository
{
    public async Task<PagedResult<NewsDto>> ListAsync(NewsQuery query, bool publicOnly, DateTime now, CancellationToken ct)
    {
        var page = new PageRequest(query.Page, query.PageSize);
        var posts = Visible(publicOnly, now).Where(p => query.Category == null || p.Category == query.Category);
        var total = await posts.CountAsync(ct);
        var items = await Project(posts.OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id).Skip(page.Skip).Take(page.SafePageSize), withBody: false)
            .ToListAsync(ct);
        return new PagedResult<NewsDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public Task<NewsDto?> GetAsync(long id, bool publicOnly, DateTime now, CancellationToken ct) =>
        Project(Visible(publicOnly, now).Where(p => p.Id == id), withBody: true).FirstOrDefaultAsync(ct);

    public Task<NewsPost?> FindAsync(long id, CancellationToken ct) => db.Set<NewsPost>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Add(NewsPost post) => db.Set<NewsPost>().Add(post);

    private IQueryable<NewsPost> Visible(bool publicOnly, DateTime now) =>
        db.Set<NewsPost>().AsNoTracking().Where(p => !publicOnly || (!p.IsHidden && p.PublishedAt <= now));

    private static IQueryable<NewsDto> Project(IQueryable<NewsPost> query, bool withBody) =>
        query.Select(p => new NewsDto(p.Id, p.Category, p.Title, p.Excerpt, withBody ? p.Body : null,
            p.ImageFileId == null ? null : "/api/v1/files/" + p.ImageFileId, p.IsBreaking, p.IsHidden, p.PublishedAt));
}
