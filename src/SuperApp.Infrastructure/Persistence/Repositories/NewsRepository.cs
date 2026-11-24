using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Favorites;
using SuperApp.Application.Features.News;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>News stories (content.NewsPosts) and who hears about them.</summary>
public sealed class NewsRepository(AppDbContext db) : INewsRepository
{
    private sealed record Row(long Id, string Title, string Body, string Language, string? SourceName, string? SourceUrl, FollowClubDto? Club,
        bool Important, DateTime PublishedAt, bool IsPublished);

    private IQueryable<NewsPost> Posts(bool publishedOnly) => db.Set<NewsPost>().AsNoTracking().Where(p => !publishedOnly || p.IsPublished);

    private static IQueryable<Row> Project(IQueryable<NewsPost> posts) =>
        posts.Select(p => new Row(p.Id, p.Title, p.Body, p.Language, p.SourceName, p.SourceUrl,
                p.Club == null ? null : new FollowClubDto(p.Club.Id, p.Club.NameEn, p.Club.NameAr, p.Club.ShortName, p.Club.LogoUrl),
                p.Important, p.PublishedAt, p.IsPublished));

    private static NewsItemDto Item(Row r) =>
        new(r.Id, r.Title, NewsService.Excerpt(r.Body, 220), r.Language, r.SourceName, r.SourceUrl, r.Club, r.Important, r.PublishedAt);

    public async Task<IReadOnlyList<NewsItemDto>> ListAsync(long? clubId, bool publishedOnly, int skip, int take, CancellationToken ct)
    {
        var rows = await Project(Posts(publishedOnly).Where(p => clubId == null || p.ClubId == clubId)
            .OrderByDescending(p => p.PublishedAt).ThenByDescending(p => p.Id).Skip(skip).Take(take)).ToListAsync(ct);
        return rows.Select(Item).ToList();
    }

    public async Task<NewsPostDto?> GetAsync(long id, bool publishedOnly, CancellationToken ct)
    {
        var row = await Project(Posts(publishedOnly).Where(p => p.Id == id)).FirstOrDefaultAsync(ct);
        return row is null ? null : new NewsPostDto(Item(row), row.Body, row.IsPublished);
    }

    public Task<NewsPost?> FindAsync(long id, CancellationToken ct) => db.Set<NewsPost>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public void Add(NewsPost post) => db.Set<NewsPost>().Add(post);

    public Task<bool> ClubExistsAsync(long clubId, CancellationToken ct) => db.Set<Club>().AnyAsync(c => c.Id == clubId, ct);

    public async Task<IReadOnlyList<Guid>> AudienceAsync(long? clubId, bool everyone, CancellationToken ct) => everyone
        ? await db.Set<User>().AsNoTracking().Where(u => u.Status == UserStatus.Active && u.AccountType == AccountType.Person).Select(u => u.Id).ToListAsync(ct)
        : await db.Set<FavoriteClub>().AsNoTracking().Where(f => f.ClubId == clubId && f.User.Status == UserStatus.Active).Select(f => f.UserId).ToListAsync(ct);
}
