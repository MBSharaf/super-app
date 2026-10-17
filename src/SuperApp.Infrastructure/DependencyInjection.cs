using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Infrastructure.Persistence;
using SuperApp.Infrastructure.Persistence.Interceptors;
using SuperApp.Infrastructure.Persistence.Seed;
using SuperApp.Infrastructure.Services;

namespace SuperApp.Infrastructure;

/// <summary>Registers the Infrastructure layer: database, Identity, and the services that talk to the outside world.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is missing. Set CONNECTIONSTRINGS__DEFAULT in .env.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<AuditableSaveChangesInterceptor>();
        services.AddDbContext<AppDbContext>((sp, options) => options
            .UseSqlServer(connection, sql => sql.EnableRetryOnFailure(3))
            // Soft delete puts a filter on parents that have required children; that is intended, so EF's warning is muted
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .AddInterceptors(sp.GetRequiredService<AuditableSaveChangesInterceptor>()));

        // Keys for cookies, reset links and anti-forgery tokens; kept on disk so sign-ins survive a restart
        var keys = configuration["Security:Data_Protection_Keys_Path"] ?? "keys";
        services.AddDataProtection().SetApplicationName("SuperApp").PersistKeysToFileSystem(new DirectoryInfo(keys));

        services.AddIdentityCore<User>(ConfigureIdentity)
            .AddRoles<Role>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddSeed(configuration);
        return services;
    }

    /// <summary>Password, lockout and account rules (PRD FR-AUTH-01, FR-AUTH-03).</summary>
    private static void ConfigureIdentity(IdentityOptions o)
    {
        o.Stores.SchemaVersion = AppDbContext.IdentitySchema;
        o.User.RequireUniqueEmail = true;
        o.Password.RequiredLength = 8;
        o.Password.RequireDigit = true;
        o.Password.RequireNonAlphanumeric = false;
        o.Password.RequireUppercase = false;
        o.Lockout.MaxFailedAccessAttempts = 5;
        o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        o.Lockout.AllowedForNewUsers = true;
        o.SignIn.RequireConfirmedEmail = false;   // confirmed later for sensitive actions; sign-in stays possible
    }
}
