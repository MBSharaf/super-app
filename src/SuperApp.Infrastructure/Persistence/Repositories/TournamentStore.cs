using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Tournaments;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

public sealed class TournamentStore(AppDbContext db) : ITournamentStore
{
    private IQueryable<Tournament> Full() => db.Set<Tournament>().AsSplitQuery()
        .Include(t => t.Pitch).Include(t => t.Partner)
        .Include(t => t.Teams).ThenInclude(x => x.Payment)
        .Include(t => t.Matches).ThenInclude(m => m.Booking);

    public async Task<IReadOnlyList<Tournament>> ListAsync(bool includeFinished, CancellationToken ct) =>
        await Full().Where(t => t.Status != TournamentStatus.Cancelled && (includeFinished || t.Status != TournamentStatus.Finished))
            .OrderBy(t => t.Status == TournamentStatus.Finished).ThenBy(t => t.StartsOn).Take(100).ToListAsync(ct);

    public async Task<IReadOnlyList<Tournament>> OfPartnerAsync(long partnerId, CancellationToken ct) =>
        await Full().Where(t => t.PartnerId == partnerId).OrderByDescending(t => t.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<Tournament>> AllAsync(CancellationToken ct) =>
        await Full().OrderByDescending(t => t.Id).Take(200).ToListAsync(ct);

    public Task<Tournament?> FindAsync(long id, CancellationToken ct) => Full().FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tournament?> ByTeamPaymentAsync(long paymentId, CancellationToken ct) =>
        Full().FirstOrDefaultAsync(t => t.Teams.Any(x => x.PaymentId == paymentId), ct);

    public void Add(Tournament tournament) => db.Set<Tournament>().Add(tournament);

    public void Remove(TournamentMatch match) => db.Set<TournamentMatch>().Remove(match);

    public async Task<IReadOnlyDictionary<Guid, string>> NamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.ToList();
        return await db.Set<User>().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
    }

    public Task<decimal?> PartnerCommissionAsync(long partnerId, CancellationToken ct) =>
        db.Set<Partner>().Where(p => p.Id == partnerId).Select(p => p.BookingCommission).FirstOrDefaultAsync(ct);
}
