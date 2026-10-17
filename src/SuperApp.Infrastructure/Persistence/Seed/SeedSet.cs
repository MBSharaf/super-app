using Microsoft.EntityFrameworkCore;

namespace SuperApp.Infrastructure.Persistence.Seed;

/// <summary>The one idempotent step every seeder uses: add the rows whose natural key is not in the table yet.</summary>
public static class SeedSet
{
    public static async Task<int> AddMissingAsync<TEntity, TKey>(
        this DbSet<TEntity> set, IEnumerable<TEntity> rows, Func<TEntity, TKey> key, CancellationToken ct)
        where TEntity : class
        where TKey : notnull
    {
        // IgnoreQueryFilters: a soft-deleted row still owns its key (unique index)
        var existing = (await set.IgnoreQueryFilters().AsNoTracking().ToListAsync(ct)).Select(key).ToHashSet();
        var missing = rows.Where(r => !existing.Contains(key(r))).ToList();
        set.AddRange(missing);
        return missing.Count;
    }
}
