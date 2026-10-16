using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Infrastructure.Persistence;
using SuperApp.Infrastructure.Persistence.Interceptors;
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
            .AddInterceptors(sp.GetRequiredService<AuditableSaveChangesInterceptor>()));

        services.AddIdentityCore<User>(ConfigureIdentity)
            .AddRoles<Role>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
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
