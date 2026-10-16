using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Persistence;

/// <summary>
/// The Super App database (SQL Server, code-first). Identity tables live in the <c>auth</c> schema; every other table
/// is configured by its own <c>IEntityTypeConfiguration</c> class in <c>Persistence/Configurations</c>.
/// Shared rules (timestamps, soft delete, row versions, authors) are applied by <see cref="ModelConventions"/>.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<User, Role, Guid, IdentityUserClaim<Guid>, UserRole, IdentityUserLogin<Guid>,
        IdentityRoleClaim<Guid>, IdentityUserToken<Guid>>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);                                  // Identity tables first
        builder.Ignore<IdentityPasskeyData>();                          // no passkeys table in schema version 2
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
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
        configurationBuilder.Properties<DateTime>().HaveColumnType("datetime2(0)");
        configurationBuilder.Properties<DateTime?>().HaveColumnType("datetime2(0)");
    }
}
