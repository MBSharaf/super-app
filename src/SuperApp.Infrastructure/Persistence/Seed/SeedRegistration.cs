using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SuperApp.Infrastructure.Persistence.Seed;

/// <summary>Registers the seed and runs it once at start-up.</summary>
public static class SeedRegistration
{
    public static IServiceCollection AddSeed(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(SeedOptions.From(configuration));
        services.AddScoped<DatabaseSeeder>();
        var seeders = typeof(ISeeder).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(ISeeder).IsAssignableFrom(t));
        foreach (var seeder in seeders)
        {
            services.AddScoped(typeof(ISeeder), seeder);
        }
        return services;
    }

    /// <summary>Runs the idempotent seed. A failure is logged and does not stop the site from starting.</summary>
    public static async Task SeedDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(SeedRegistration));
        try
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().RunAsync(ct);
        }
#pragma warning disable CA1031 // start-up must survive a seed problem; the error is logged with details
        catch (Exception ex)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "Seed failed — the site keeps running; fix the cause and restart to run the seed again.");
        }
    }
}
