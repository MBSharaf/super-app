using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Money;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Payments;

/// <summary>Platform revenue from money.LedgerEntries (the PlatformRevenue wallet), money.Payments and fantasy passes.</summary>
public sealed class RevenueReader(AppDbContext db) : IRevenueReader
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    public async Task<(IReadOnlyList<RevenueDayDto> Days, IReadOnlyList<RevenueOwnerDto> Owners, IReadOnlyList<RevenuePassDto> Passes)> ReadAsync(
        DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var rows = await db.Set<LedgerEntry>().AsNoTracking()
            .Where(e => e.Wallet.Type == WalletType.PlatformRevenue && e.CreatedAt >= fromUtc && e.CreatedAt < toUtc)
            .Select(e => new { e.CreatedAt, e.Kind, e.RefType, e.Amount }).ToListAsync(ct);
        var paid = await db.Set<Payment>().AsNoTracking()
            .Where(p => p.PaidAt >= fromUtc && p.PaidAt < toUtc && p.Purpose != PaymentPurpose.WalletTopUp && p.Status != PaymentStatus.Failed)
            .Select(p => new { At = p.PaidAt!.Value, p.Amount }).ToListAsync(ct);
        var days = rows.Select(r => (Day: DayOf(r.CreatedAt), r.Kind, r.RefType, r.Amount)).GroupBy(r => r.Day)
            .Select(g => (g.Key, Bookings: g.Where(r => r.Kind == LedgerKind.Commission).Sum(r => r.Amount),
                Fees: g.Where(r => r.Kind == LedgerKind.ServiceFee && r.RefType != "FantasyPass").Sum(r => r.Amount),
                Passes: g.Where(r => r.Kind == LedgerKind.ServiceFee && r.RefType == "FantasyPass").Sum(r => r.Amount),
                Refunds: -g.Where(r => r.Kind == LedgerKind.Refund).Sum(r => r.Amount),
                Promos: -g.Where(r => r.Kind == LedgerKind.Voucher).Sum(r => r.Amount)))
            .ToDictionary(x => x.Key);
        var gmv = paid.GroupBy(p => DayOf(p.At)).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var list = days.Keys.Union(gmv.Keys).Select(d =>
        {
            var x = days.GetValueOrDefault(d);
            return new RevenueDayDto(d, x.Bookings, x.Fees, x.Passes, x.Refunds, x.Promos, gmv.GetValueOrDefault(d));
        }).ToList();
        return (list, await OwnersAsync(fromUtc, toUtc, ct), await PassesAsync(fromUtc, toUtc, ct));
    }

    /// <summary>Owners by booking commission booked into the revenue wallet in the period (top 6).</summary>
    private async Task<IReadOnlyList<RevenueOwnerDto>> OwnersAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var lines = await (from e in db.Set<LedgerEntry>().AsNoTracking()
                           where e.Wallet.Type == WalletType.PlatformRevenue && e.Kind == LedgerKind.Commission && e.RefType == "Booking" && e.CreatedAt >= fromUtc && e.CreatedAt < toUtc
                           join b in db.Set<Booking>().IgnoreQueryFilters() on e.RefId equals b.Id
                           join p in db.Set<Pitch>().IgnoreQueryFilters() on b.PitchId equals p.Id
                           select new { b.Id, Owner = p.Venue.Partner.BusinessName, b.Total, e.Amount }).ToListAsync(ct);
        return lines.GroupBy(l => l.Owner)
            .Select(g => new RevenueOwnerDto(g.Key, g.Select(l => l.Id).Distinct().Count(), g.GroupBy(l => l.Id).Sum(b => b.First().Total), g.Sum(l => l.Amount)))
            .OrderByDescending(o => o.Commission).Take(6).ToList();
    }

    private async Task<IReadOnlyList<RevenuePassDto>> PassesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
        (await db.Set<FantasyPass>().AsNoTracking()
            .Where(p => p.Payment != null && p.Payment.Status == PaymentStatus.Paid && p.Payment.PaidAt >= fromUtc && p.Payment.PaidAt < toUtc)
            .OrderByDescending(p => p.Payment!.PaidAt).Take(500)
            .Select(p => new { Key = p.Competition == null ? null : p.Competition.Key, p.Price, At = p.Payment!.PaidAt!.Value }).ToListAsync(ct))
        .Select(p => new RevenuePassDto(p.Key?.ToLowerInvariant() ?? "both", p.Price, p.At)).ToList();

    private static DateOnly DayOf(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Cairo));
}
