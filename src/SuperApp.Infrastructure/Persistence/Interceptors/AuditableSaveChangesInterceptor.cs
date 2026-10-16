using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Common;

namespace SuperApp.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Before every save: fills <c>CreatedAt</c> / <c>UpdatedAt</c>, records the author, and turns deletes of
/// soft-deletable rows into updates (<c>IsDeleted = 1</c>) so business history is never lost.
/// </summary>
public sealed class AuditableSaveChangesInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }
        var now = clock.UtcNow;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            StampTimes(entry, now);
            StampAuthor(entry);
            SoftDelete(entry, now);
        }
    }

    private static void StampTimes(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, DateTime now)
    {
        if (entry.Entity is not ITimestamped stamped)
        {
            return;
        }
        if (entry.State == EntityState.Added && stamped.CreatedAt == default)
        {
            stamped.CreatedAt = now;
        }
        else if (entry.State == EntityState.Modified)
        {
            stamped.UpdatedAt = now;
        }
    }

    private void StampAuthor(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
    {
        if (entry.State == EntityState.Added && entry.Entity is IHasAuthor authored && authored.CreatedById is null)
        {
            authored.CreatedById = currentUser.UserId;
        }
    }

    private static void SoftDelete(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, DateTime now)
    {
        if (entry.State == EntityState.Deleted && entry.Entity is ISoftDelete deletable)
        {
            entry.State = EntityState.Modified;
            deletable.IsDeleted = true;
            deletable.DeletedAt = now;
        }
    }
}
