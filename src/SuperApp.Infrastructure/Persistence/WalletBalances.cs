using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using SuperApp.Domain.Entities.Money;

namespace SuperApp.Infrastructure.Persistence;

/// <summary>
/// Wallet balances are running totals that many payments change at the same moment (Super Malaab's revenue wallet is
/// touched by every booking). Instead of writing the new total (which made the later of two payments fail with
/// 409 stale), a save adds this request's change to the stored total in one SQL statement:
/// <c>AvailableBalance = AvailableBalance + @change</c>. The database check (no negative personal balance) still applies.
/// </summary>
internal sealed class WalletBalances
{
    private readonly List<(EntityEntry<Wallet> Entry, decimal Pending, decimal Available, decimal Settled)> changes = [];

    private static readonly string[] Columns = [nameof(Wallet.PendingBalance), nameof(Wallet.AvailableBalance), nameof(Wallet.SettledTotal)];

    public bool Any => changes.Count > 0;

    /// <summary>Takes the balance changes out of the normal save (the columns are written by <see cref="ApplyAsync"/>).</summary>
    public static WalletBalances Take(ChangeTracker tracker)
    {
        var taken = new WalletBalances();
        foreach (var entry in tracker.Entries<Wallet>().Where(e => e.State == EntityState.Modified))
        {
            var change = (entry, Delta(entry, Columns[0]), Delta(entry, Columns[1]), Delta(entry, Columns[2]));
            if (change.Item2 == 0 && change.Item3 == 0 && change.Item4 == 0)
            {
                continue;
            }
            taken.changes.Add(change);
            foreach (var column in Columns)
            {
                entry.Property(column).IsModified = false;
            }
        }
        return taken;
    }

    /// <summary>Adds each change to the stored totals (inside the caller's transaction).</summary>
    public async Task ApplyAsync(DbContext db, DateTime now, CancellationToken ct)
    {
        foreach (var (entry, pending, available, settled) in changes)
        {
            var id = entry.Entity.Id;
            await db.Set<Wallet>().IgnoreQueryFilters().Where(w => w.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(w => w.PendingBalance, w => w.PendingBalance + pending)
                .SetProperty(w => w.AvailableBalance, w => w.AvailableBalance + available)
                .SetProperty(w => w.SettledTotal, w => w.SettledTotal + settled)
                .SetProperty(w => w.UpdatedAt, now), ct);
        }
    }

    /// <summary>After the commit: the tracked wallets show the stored totals (and row version).</summary>
    public async Task RefreshAsync(CancellationToken ct)
    {
        foreach (var (entry, _, _, _) in changes)
        {
            await entry.ReloadAsync(ct);
        }
    }

    /// <summary>The save failed: put the changes back so the tracked wallets are as the caller left them.</summary>
    public void Restore()
    {
        foreach (var (entry, _, _, _) in changes)
        {
            foreach (var column in Columns)
            {
                entry.Property(column).IsModified = true;
            }
        }
    }

    private static decimal Delta(EntityEntry<Wallet> entry, string column) =>
        entry.Property<decimal>(column).CurrentValue - entry.Property<decimal>(column).OriginalValue;
}
