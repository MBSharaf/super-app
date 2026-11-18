using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Persistence;

/// <summary>
/// The Super Malaab database (SQL Server, code-first). Identity tables live in the <c>auth</c> schema; every other table
/// is configured by its own <c>IEntityTypeConfiguration</c> class in <c>Persistence/Configurations</c>.
/// Shared rules (timestamps, soft delete, row versions, authors) are applied by <see cref="ModelConventions"/>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<User, Role, Guid, IdentityUserClaim<Guid>, UserRole, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options)
{
    /// <summary>
    /// Saves; wallet balances are added to the stored totals in the same transaction (see <see cref="WalletBalances"/>),
    /// so payments changing one wallet at the same moment never overwrite each other.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        var balances = WalletBalances.Take(ChangeTracker);
        if (!balances.Any)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        try
        {
            var saved = Database.CurrentTransaction is not null
                ? await SaveWithBalancesAsync(balances, cancellationToken)
                : await Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
                    var count = await SaveWithBalancesAsync(balances, cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return count;
                });
            if (acceptAllChangesOnSuccess)
            {
                ChangeTracker.AcceptAllChanges();
            }
            await balances.RefreshAsync(cancellationToken);
            return saved;
        }
        catch
        {
            balances.Restore();
            throw;
        }
    }

    private async Task<int> SaveWithBalancesAsync(WalletBalances balances, CancellationToken ct)
    {
        var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
        await balances.ApplyAsync(this, DateTime.UtcNow, ct);
        return saved;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);                                  // Identity tables first
        builder.Ignore<IdentityPasskeyData>();                          // no passkeys table in schema version 2
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        Configurations.Money.ReceiptSequences.Apply(builder);          // receipt numbers (money schema)
        builder.ApplySharedConventions();                               // after every configuration
    }

    /// <summary>
    /// Identity schema version used everywhere (runtime and design time): version 2 has no passkeys table,
    /// so the tables match the ERD. Passkeys (passwordless sign-in) can be enabled later by moving to version 3.
    /// </summary>
    public static readonly Version IdentitySchema = IdentitySchemaVersions.Version2;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Dates are stored in UTC without fractions unless a column says otherwise (ERD conventions)
        // Values read back are marked UTC, so the API sends "…Z" and browsers show the right local time
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(0)").HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveColumnType("datetime2(0)").HaveConversion<UtcDateTimeConverter>();
    }
}

/// <summary>
/// Stores DateTime in UTC and reads it back with Kind = Utc (SQL Server keeps no time zone). Columns are datetime2(0):
/// the fraction of a second is cut off here, because SQL Server would round it up and a time stored "now" could land
/// up to half a second in the future.
/// </summary>
public sealed class UtcDateTimeConverter() : Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTime, DateTime>(
    v => new DateTime((v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v).Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
