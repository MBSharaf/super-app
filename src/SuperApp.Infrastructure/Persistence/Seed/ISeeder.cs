namespace SuperApp.Infrastructure.Persistence.Seed;

/// <summary>
/// One area of the seed (ERD §12). Seeders run by <see cref="Order"/> and must be idempotent: they look up
/// rows by their natural key (Key, Code, Email) and only add what is missing, so the seed can run many times.
/// </summary>
public interface ISeeder
{
    /// <summary>Position in the seed order (ERD §12 table).</summary>
    int Order { get; }

    /// <summary>True for the demo data that is skipped when <c>SEED__DEMODATA=false</c>.</summary>
    bool IsDemo => false;

    /// <summary>Adds the missing rows and returns how many were added.</summary>
    Task<int> SeedAsync(CancellationToken ct);
}
