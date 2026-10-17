using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SuperApp.Infrastructure.Persistence.Seed;

/// <summary>
/// Runs every <see cref="ISeeder"/> in order, each in its own transaction (ERD §12). It does nothing until the
/// database exists and is up to date — the owner runs Add-Migration / Update-Database first.
/// </summary>
public sealed class DatabaseSeeder(AppDbContext db, IEnumerable<ISeeder> seeders, SeedOptions options, ILogger<DatabaseSeeder> logger)
{
    /// <summary>Returns false when the seed was skipped because the database is not ready.</summary>
    public async Task<bool> RunAsync(CancellationToken ct = default)
    {
        if (!await IsReadyAsync(ct))
        {
            return false;
        }
        foreach (var seeder in seeders.OrderBy(s => s.Order))
        {
            if (seeder.IsDemo && !options.DemoData)
            {
                continue;
            }
            var added = await RunOneAsync(seeder, ct);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Seed {Order} {Seeder}: {Added} rows added", seeder.Order, seeder.GetType().Name, added);
            }
        }
        return true;
    }

    private async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!await db.Database.CanConnectAsync(ct))
        {
            logger.LogWarning("Seed skipped: the database does not exist yet. Run Add-Migration and Update-Database first.");
            return false;
        }
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            logger.LogWarning("Seed skipped: {Count} migration(s) not applied ({First}…). Run Update-Database first.", pending.Count, pending[0]);
            return false;
        }
        return true;
    }

    private Task<int> RunOneAsync(ISeeder seeder, CancellationToken ct)
    {
        // The SQL Server retry strategy needs user transactions to run inside it
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var added = await seeder.SeedAsync(ct);
            await transaction.CommitAsync(ct);
            return added;
        });
    }
}
