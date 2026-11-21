using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.AdminConsole;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Admin;

/// <summary>
/// Pitch accounts: the bookings give the booked value and where it was paid; the ledger gives what Super Malaab kept and
/// what it owes each venue (refunds included); the partner's wallet and payouts give what is owed now.
/// </summary>
public sealed class SettlementsReader(AppDbContext db) : ISettlementsReader
{
    private static readonly BookingStatus[] Counted = [BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Played, BookingStatus.NoShow];

    public async Task<IReadOnlyList<PitchSettlementRow>> PitchesAsync(DateOnly from, DateOnly to, string? q, CancellationToken ct)
    {
        var inRange = db.Set<Booking>().Where(b => b.Date >= from && b.Date <= to);
        var stats = await inRange.Where(b => Counted.Contains(b.Status)).GroupBy(b => b.PitchId).Select(g => new
        {
            PitchId = g.Key,
            Count = g.Count(),
            Gross = g.Sum(b => b.Total),
            InApp = g.Sum(b => b.PayAtVenue ? 0 : b.Total - b.BalanceDue),
            Cash = g.Sum(b => b.PayAtVenue ? b.Total : b.BalanceDue),
            Still = g.Sum(b => b.BalancePaidAt == null && b.Status != BookingStatus.Played && b.Status != BookingStatus.CheckedIn ? (b.PayAtVenue ? b.Total : b.BalanceDue) : 0),
        }).ToDictionaryAsync(x => x.PitchId, ct);
        var money = await LedgerAsync(inRange, ct);
        var ids = stats.Keys.Union(money.Keys).ToList();
        var pitches = db.Set<Pitch>().IgnoreQueryFilters().Where(p => ids.Contains(p.Id));
        if (!string.IsNullOrEmpty(q))
        {
            pitches = pitches.Where(p => p.Name.Contains(q) || p.Venue.Name.Contains(q) || p.Venue.Partner.BusinessName.Contains(q));
        }
        var info = await pitches.Select(p => new { p.Id, p.Name, Venue = p.Venue.Name, p.Venue.PartnerId, Partner = p.Venue.Partner.BusinessName, p.PaymentMode }).ToListAsync(ct);
        return [.. info.Select(p =>
        {
            var s = stats.GetValueOrDefault(p.Id);
            var m = money.GetValueOrDefault(p.Id);
            return new PitchSettlementRow(p.Id, p.Name, p.Venue, p.PartnerId, p.Partner, p.PaymentMode.ToString(), s?.Count ?? 0, s?.Gross ?? 0, s?.InApp ?? 0,
                s?.Cash ?? 0, s?.Still ?? 0, m.Ours, m.Venue);
        }).OrderByDescending(r => r.Gross).ThenBy(r => r.PitchName)];
    }

    /// <summary>Per pitch: the ledger rows of its bookings in the range on Super Malaab's revenue wallet and on the partners' booking wallets.</summary>
    private async Task<Dictionary<long, (decimal Ours, decimal Venue)>> LedgerAsync(IQueryable<Booking> bookings, CancellationToken ct)
    {
        var rows = await db.Set<LedgerEntry>()
            .Where(e => e.RefType == "Booking" && e.Kind != LedgerKind.ReleaseFromHold
                && (e.Wallet.Type == WalletType.PlatformRevenue || e.Wallet.Type == WalletType.PartnerBookings))
            .Join(bookings, e => e.RefId, b => b.Id, (e, b) => new { b.PitchId, e.Wallet.Type, e.Amount })
            .GroupBy(x => new { x.PitchId, x.Type })
            .Select(g => new { g.Key.PitchId, g.Key.Type, Sum = g.Sum(x => x.Amount) })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.PitchId).ToDictionary(g => g.Key, g => (
            g.Where(r => r.Type == WalletType.PlatformRevenue).Sum(r => r.Sum),
            g.Where(r => r.Type == WalletType.PartnerBookings).Sum(r => r.Sum)));
    }

    public async Task<IReadOnlyList<PartnerSettlementRow>> PartnersAsync(IReadOnlyList<PitchSettlementRow> pitches, CancellationToken ct)
    {
        var ids = pitches.Select(p => p.PartnerId).Distinct().ToList();
        var partners = await db.Set<Partner>().IgnoreQueryFilters().Where(p => ids.Contains(p.Id)).Select(p => new { p.Id, p.UserId, p.BusinessName }).ToListAsync(ct);
        var owners = partners.Select(p => p.UserId).ToList();
        var wallets = await db.Set<Wallet>().Where(w => w.Type == WalletType.PartnerBookings && w.OwnerUserId != null && owners.Contains(w.OwnerUserId.Value))
            .Select(w => new { Owner = w.OwnerUserId!.Value, w.Id, w.PendingBalance, w.AvailableBalance }).ToListAsync(ct);
        var walletIds = wallets.Select(w => w.Id).ToList();
        var payouts = await db.Set<PayoutRequest>().Where(p => walletIds.Contains(p.WalletId) && p.Status != PayoutStatus.Rejected && p.Status != PayoutStatus.Failed)
            .GroupBy(p => new { p.WalletId, Sent = p.Status == PayoutStatus.Sent })
            .Select(g => new { g.Key.WalletId, g.Key.Sent, Sum = g.Sum(p => p.Amount) }).ToListAsync(ct);
        return [.. partners.Select(p =>
        {
            var mine = pitches.Where(x => x.PartnerId == p.Id).ToList();
            var w = wallets.FirstOrDefault(x => x.Owner == p.UserId);
            var paid = payouts.Where(x => x.WalletId == w?.Id && x.Sent).Sum(x => x.Sum);
            var asked = payouts.Where(x => x.WalletId == w?.Id && !x.Sent).Sum(x => x.Sum);
            var held = w?.PendingBalance ?? 0;
            var available = w?.AvailableBalance ?? 0;
            var modes = string.Join(" + ", mine.Select(x => SettlementsService.ModeLabel(x.PaymentMode)).Distinct());
            return new PartnerSettlementRow(p.Id, p.BusinessName, mine.Count, modes, mine.Sum(x => x.VenueShareInApp), held, available, asked, paid, held + available);
        }).OrderByDescending(r => r.Owed).ThenBy(r => r.PartnerName)];
    }
}
