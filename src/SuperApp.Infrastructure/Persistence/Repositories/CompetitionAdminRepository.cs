using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>The followed competitions with how many matches of their current season are cached.</summary>
public sealed class CompetitionAdminRepository(AppDbContext db) : ICompetitionAdminRepository
{
    public async Task<IReadOnlyList<AdminCompetitionDto>> ListAsync(CancellationToken ct) =>
        await db.Set<Competition>().AsNoTracking().OrderBy(c => c.SortOrder).ThenBy(c => c.Id).Select(c => new AdminCompetitionDto(
            c.Key, c.NameEn, c.NameAr, c.ShortName, c.Region, c.Color, c.Provider, c.ExternalLeagueId, c.FantasyEnabled, c.IsActive, c.SortOrder,
            db.Set<Fixture>().Count(f => f.Gameweek.Season.CompetitionId == c.Id && f.Gameweek.Season.IsCurrent),
            db.Set<Season>().Where(s => s.CompetitionId == c.Id && s.IsCurrent).Select(s => s.LastSyncedAt).FirstOrDefault())).ToListAsync(ct);

    public Task<Competition?> FindAsync(string key, CancellationToken ct) => db.Set<Competition>().FirstOrDefaultAsync(c => c.Key == key, ct);

    public Task<bool> TakenAsync(string key, string provider, int externalLeagueId, CancellationToken ct) =>
        db.Set<Competition>().AnyAsync(c => c.Key == key || (c.Provider == provider && c.ExternalLeagueId == externalLeagueId), ct);

    public async Task<int> NextSortOrderAsync(CancellationToken ct) => (await db.Set<Competition>().MaxAsync(c => (int?)c.SortOrder, ct) ?? 0) + 1;

    public void Add(Competition competition) => db.Set<Competition>().Add(competition);
}
