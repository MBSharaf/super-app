using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Money;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Payments;

public sealed class MoneyStore(AppDbContext db, IClock clock) : IMoneyStore
{
    public async Task<Wallet> WalletAsync(Guid? ownerUserId, WalletType type, CancellationToken ct)
    {
        var wallet = db.Set<Wallet>().Local.FirstOrDefault(w => w.OwnerUserId == ownerUserId && w.Type == type)
            ?? await db.Set<Wallet>().FirstOrDefaultAsync(w => w.OwnerUserId == ownerUserId && w.Type == type, ct);
        if (wallet is null)
        {
            wallet = new Wallet { OwnerUserId = ownerUserId, Type = type };
            db.Set<Wallet>().Add(wallet);
        }
        return wallet;
    }

    public void Add(LedgerEntry entry) => db.Set<LedgerEntry>().Add(entry);

    public void Add(Refund refund) => db.Set<Refund>().Add(refund);

    public void Add(Receipt receipt) => db.Set<Receipt>().Add(receipt);

    /// <summary>RC-2026-000123 (payment), RF- (refund), PA- (payout advice)…: year + running number of that kind.</summary>
    public async Task<string> NewReceiptNumberAsync(ReceiptKind kind, CancellationToken ct)
    {
        var prefix = kind switch { ReceiptKind.Refund => "RF", ReceiptKind.PayoutAdvice => "PA", ReceiptKind.TopUp => "TU", ReceiptKind.Statement => "ST", _ => "RC" };
        var head = $"{prefix}-{clock.UtcNow.Year}-";
        var saved = await db.Set<Receipt>().CountAsync(r => r.Number.StartsWith(head), ct);
        var pending = db.Set<Receipt>().Local.Count(r => r.Id == 0 && r.Number.StartsWith(head, StringComparison.Ordinal));
        return head + (saved + pending + 1).ToString("000000", CultureInfo.InvariantCulture);
    }

    public Task<Guid> PitchOwnerAsync(long pitchId, CancellationToken ct) =>
        db.Set<Pitch>().IgnoreQueryFilters().Where(p => p.Id == pitchId).Select(p => p.Venue.Partner.UserId).FirstAsync(ct);

    public Task<Payment?> PaymentAsync(long paymentId, CancellationToken ct) => db.Set<Payment>().FirstOrDefaultAsync(p => p.Id == paymentId, ct);

    public async Task<IReadOnlyList<LedgerEntry>> EntriesForAsync(string refType, long refId, CancellationToken ct) =>
        await db.Set<LedgerEntry>().Include(e => e.Wallet).Where(e => e.RefType == refType && e.RefId == refId).ToListAsync(ct);

    public async Task<IReadOnlyList<WalletDto>> WalletsOfAsync(Guid userId, CancellationToken ct) =>
        await db.Set<Wallet>().Where(w => w.OwnerUserId == userId).OrderBy(w => w.Type)
            .Select(w => new WalletDto(w.Type.ToString(), w.PendingBalance, w.AvailableBalance, w.SettledTotal, w.Currency)).ToListAsync(ct);

    public async Task<PagedResult<LedgerRowDto>> LedgerOfAsync(Guid userId, WalletType type, int page, int pageSize, CancellationToken ct)
    {
        var rows = db.Set<LedgerEntry>().Where(e => e.Wallet.OwnerUserId == userId && e.Wallet.Type == type);
        var items = await rows.OrderByDescending(e => e.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(e => new LedgerRowDto(e.Id, e.Kind.ToString(), e.Bucket.ToString(), e.Amount, e.RefType, e.RefId, e.AvailableAt, e.CreatedAt)).ToListAsync(ct);
        return new PagedResult<LedgerRowDto>(items, page, pageSize, await rows.CountAsync(ct));
    }

    public Task<IReadOnlyList<ReceiptDto>> ReceiptsOfAsync(Guid userId, CancellationToken ct) => ReceiptsAsync(userId, null, ct);

    public Task HoldUntilAsync(string refType, IReadOnlyList<long> refIds, DateTime? until, CancellationToken ct) =>
        db.Set<LedgerEntry>().Where(e => e.RefType == refType && refIds.Contains(e.RefId) && e.Bucket == LedgerBucket.Pending && e.Kind != LedgerKind.ReleaseFromHold)
            .ExecuteUpdateAsync(u => u.SetProperty(e => e.AvailableAt, until), ct);

    public async Task<decimal> StaffLimitAsync(Guid userId, StaffLimitKind kind, CancellationToken ct) =>
        await db.Set<StaffLimit>().Where(l => l.UserId == userId)
            .Select(l => (decimal?)(kind == StaffLimitKind.Credit ? l.CreditLimit : kind == StaffLimitKind.Payout ? l.PayoutLimit : l.RefundLimit)).FirstOrDefaultAsync(ct) ?? 0;

    public async Task<decimal> AgentMoneyLimitAsync(long agentId, CancellationToken ct) =>
        await db.Set<Agent>().Where(a => a.Id == agentId).Select(a => (decimal?)a.MoneyLimit).FirstOrDefaultAsync(ct) ?? 0;

    public async Task<ReceiptDto?> ReceiptAsync(Guid userId, string number, CancellationToken ct) { var list = await ReceiptsAsync(userId, number, ct); return list.Count > 0 ? list[0] : null; }

    private async Task<IReadOnlyList<ReceiptDto>> ReceiptsAsync(Guid userId, string? number, CancellationToken ct)
    {
        var rows = await db.Set<Receipt>().Where(r => r.UserId == userId && (number == null || r.Number == number))
            .OrderByDescending(r => r.Id).Take(200).ToListAsync(ct);
        var ids = rows.Where(r => r.RefType == "Booking").Select(r => r.RefId).Distinct().ToList();
        var codes = await db.Set<Booking>().Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.Code, ct);
        var payoutIds = rows.Where(r => r.RefType == "Payout").Select(r => r.RefId).Distinct().ToList();
        var payouts = await db.Set<PayoutRequest>().Where(p => payoutIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        var topUpIds = rows.Where(r => r.RefType == Ledger.TopUpRef).Select(r => r.RefId).Distinct().ToList();
        var topUps = await db.Set<Payment>().Where(p => topUpIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        return rows.Select(r => new ReceiptDto(r.Number, r.Kind.ToString(), r.Amount, r.RefType,
            (r.RefType switch { "Payout" => payouts, Ledger.TopUpRef => topUps, _ => codes }).GetValueOrDefault(r.RefId), r.CreatedAt)).ToList();
    }
}

/// <summary>
/// Every 10 minutes: pending money whose hold has ended (24 hours after the game) moves to Available, per wallet and
/// booking — net of refunds, once. Off in "Testing" (tests call <see cref="RunOnceAsync"/>).
/// </summary>
public sealed class MoneyReleaseJob(IServiceScopeFactory scopes, IHostEnvironment environment, ILogger<MoneyReleaseJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Releasing held money failed; trying again shortly.");
            }
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var entries = db.Set<LedgerEntry>();
        var due = await entries.Where(e => e.Bucket == LedgerBucket.Pending && e.Kind != LedgerKind.ReleaseFromHold)
            .GroupBy(e => new { e.WalletId, e.RefType, e.RefId })
            .Where(g => g.Max(e => e.AvailableAt) <= now && g.Sum(e => e.Amount) != 0
                && !entries.Any(r => r.Kind == LedgerKind.ReleaseFromHold && r.WalletId == g.Key.WalletId && r.RefType == g.Key.RefType && r.RefId == g.Key.RefId))
            .Select(g => new { g.Key.WalletId, g.Key.RefType, g.Key.RefId, Amount = g.Sum(e => e.Amount) }).Take(500).ToListAsync(ct);
        var released = 0;
        // Earnings first, then debts (a store's share of a return it couldn't pay at once) once its available balance covers them
        foreach (var d in due.OrderByDescending(d => d.Amount))
        {
            var wallet = await db.Set<Wallet>().FirstAsync(w => w.Id == d.WalletId, ct);
            if (d.Amount < 0 && wallet.AvailableBalance < -d.Amount)
            {
                continue;
            }
            released++;
            var group = Guid.NewGuid();
            entries.Add(new LedgerEntry { WalletId = d.WalletId, Amount = -d.Amount, Bucket = LedgerBucket.Pending, Kind = LedgerKind.ReleaseFromHold, RefType = d.RefType, RefId = d.RefId, GroupId = group, CreatedAt = now });
            entries.Add(new LedgerEntry { WalletId = d.WalletId, Amount = d.Amount, Bucket = LedgerBucket.Available, Kind = LedgerKind.ReleaseFromHold, RefType = d.RefType, RefId = d.RefId, GroupId = group, CreatedAt = now });
            wallet.PendingBalance -= d.Amount;
            wallet.AvailableBalance += d.Amount;
        }
        await db.SaveChangesAsync(ct);
        return released;
    }
}
