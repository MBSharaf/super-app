using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Persistence;

/// <summary>
/// Rules shared by many tables, applied once instead of in every configuration (ERD §1.3 column sets):
/// [B] CreatedAt default · [L] IsActive / SortOrder defaults · [S] soft delete filter · [V] row version · [A] author FK.
/// </summary>
internal static class ModelConventions
{
    private const string UtcNow = "SYSUTCDATETIME()";

    public static void ApplySharedConventions(this ModelBuilder builder)
    {
        foreach (var entity in builder.Model.GetEntityTypes().Where(e => !e.IsOwned()).ToList())
        {
            var type = entity.ClrType;
            var b = builder.Entity(type);
            if (entity.FindProperty("CreatedAt")?.ClrType == typeof(DateTime))
            {
                b.Property<DateTime>("CreatedAt").HasDefaultValueSql(UtcNow).ValueGeneratedNever();
            }
            if (typeof(LookupEntity).IsAssignableFrom(type))
            {
                b.Property<bool>(nameof(LookupEntity.IsActive)).HasDefaultValue(true).ValueGeneratedNever();
                b.Property<int>(nameof(LookupEntity.SortOrder)).HasDefaultValue(0).ValueGeneratedNever();
            }
            if (typeof(ISoftDelete).IsAssignableFrom(type))
            {
                b.Property<bool>(nameof(ISoftDelete.IsDeleted)).HasDefaultValue(false).ValueGeneratedNever();
                b.HasQueryFilter(SoftDeleteFilter(type));
            }
            if (typeof(IHasRowVersion).IsAssignableFrom(type))
            {
                b.Property<byte[]>(nameof(IHasRowVersion.RowVersion)).IsRowVersion();
            }
            if (typeof(IHasAuthor).IsAssignableFrom(type))
            {
                b.HasOne(typeof(User)).WithMany().HasForeignKey(nameof(IHasAuthor.CreatedById)).OnDelete(DeleteBehavior.NoAction);
            }
        }
    }

    /// <summary>Builds <c>x =&gt; !x.IsDeleted</c> for the entity type.</summary>
    private static LambdaExpression SoftDeleteFilter(Type type)
    {
        var x = Expression.Parameter(type, "x");
        var isDeleted = Expression.Property(x, nameof(ISoftDelete.IsDeleted));
        return Expression.Lambda(Expression.Not(isDeleted), x);
    }
}
