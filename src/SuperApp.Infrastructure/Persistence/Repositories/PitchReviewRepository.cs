using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Partners;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Pitches for the admin's review screen (oldest waiting first).</summary>
public sealed class PitchReviewRepository(AppDbContext db) : IPitchReviewRepository
{
    public async Task<PagedResult<PitchReviewRowDto>> ListAsync(PitchStatus status, PageRequest page, CancellationToken ct)
    {
        var query = db.Set<Pitch>().Where(p => p.Status == status);
        var total = await query.CountAsync(ct);
        var rows = await Rows(query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Skip(page.Skip).Take(page.SafePageSize)).ToListAsync(ct);
        return new PagedResult<PitchReviewRowDto>(rows.Select(r => r.Row).ToList(), page.SafePage, page.SafePageSize, total);
    }

    public async Task<(PitchReviewRowDto Row, string Address, PitchOwner Owner)?> SummaryAsync(long pitchId, CancellationToken ct)
    {
        var r = await Rows(db.Set<Pitch>().Where(p => p.Id == pitchId)).FirstOrDefaultAsync(ct);
        return r is null ? null : (r.Row, r.Address, r.Owner);
    }

    public Task<Pitch?> FindAsync(long pitchId, CancellationToken ct) => db.Set<Pitch>().FirstOrDefaultAsync(p => p.Id == pitchId, ct);

    private sealed record Raw(PitchReviewRowDto Row, string Address, PitchOwner Owner);

    private static IQueryable<Raw> Rows(IQueryable<Pitch> q) => q.Select(p => new Raw(
        new PitchReviewRowDto(p.Id, p.Slug, p.Name, p.Venue.Partner.BusinessName, p.Venue.Name, p.Venue.Area.NameEn, p.Format, p.PricePerHour, p.Status.ToString(),
            p.Photos.Count, p.CreatedAt),
        p.Venue.Address,
        new PitchOwner(p.Venue.PartnerId, p.Venue.Partner.UserId, p.Venue.Partner.BusinessName)));
}
