using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Favorites;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Followed clubs (fantasy.FavoriteClubs) and the clubs that can be followed (current seasons of active competitions).</summary>
public sealed class FavoriteClubsRepository(AppDbContext db) : IFavoriteClubsRepository
{
    private IQueryable<SeasonClub> Playing() =>
        db.Set<SeasonClub>().AsNoTracking().Where(sc => sc.Season.IsCurrent && sc.Season.Competition.IsActive);

    public async Task<IReadOnlyList<FollowCompetitionDto>> PickableAsync(CancellationToken ct)
    {
        var rows = await Playing()
            .Select(sc => new
            {
                sc.Season.Competition.Key, sc.Season.Competition.NameEn, CompAr = sc.Season.Competition.NameAr, sc.Season.Competition.SortOrder,
                Club = new FollowClubDto(sc.ClubId, sc.Club.NameEn, sc.Club.NameAr, sc.Club.ShortName, sc.Club.LogoUrl),
            })
            .ToListAsync(ct);
        return rows.GroupBy(r => new { r.Key, r.NameEn, r.CompAr, r.SortOrder })
            .OrderBy(g => g.Key.SortOrder)
            .Select(g => new FollowCompetitionDto(g.Key.Key, g.Key.NameEn, g.Key.CompAr,
                g.Select(r => r.Club).DistinctBy(c => c.Id).OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<long>> PickableIdsAsync(IReadOnlyList<long> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await Playing().Where(sc => ids.Contains(sc.ClubId)).Select(sc => sc.ClubId).Distinct().ToListAsync(ct);

    public async Task<IReadOnlyList<FollowClubDto>> MineAsync(Guid userId, CancellationToken ct) =>
        await db.Set<FavoriteClub>().AsNoTracking().Where(f => f.UserId == userId).OrderBy(f => f.Rank)
            .Select(f => new FollowClubDto(f.ClubId, f.Club.NameEn, f.Club.NameAr, f.Club.ShortName, f.Club.LogoUrl))
            .ToListAsync(ct);

    public async Task ReplaceAsync(Guid userId, IReadOnlyList<long> clubIds, DateTime now, CancellationToken ct)
    {
        var since = await db.Set<FavoriteClub>().AsNoTracking().Where(f => f.UserId == userId).ToDictionaryAsync(f => f.ClubId, f => f.CreatedAt, ct);
        await db.Set<FavoriteClub>().Where(f => f.UserId == userId).ExecuteDeleteAsync(ct);   // ranks are unique per person
        db.Set<FavoriteClub>().AddRange(clubIds.Select((id, i) => new FavoriteClub
        {
            UserId = userId, ClubId = id, Rank = (byte)(i + 1), CreatedAt = since.TryGetValue(id, out var at) ? at : now,
        }));
    }
}
