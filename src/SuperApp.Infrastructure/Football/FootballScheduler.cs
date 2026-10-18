using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// Runs the football sync on a schedule that fits the free plan (INTEGRATIONS §7.2): every 6 hours, and every 10 minutes
/// only while one of our matches is being played or a finished match still waits for its statistics. Off in the
/// "Testing" environment and when <c>Football:Scheduler:Enabled</c> is false.
/// Note for the main window: when Hangfire is wired up (jobs dashboard), this loop becomes a recurring job calling
/// <see cref="IFootballSyncService.SyncAsync"/> with the same rule.
/// </summary>
public sealed class FootballScheduler(IServiceScopeFactory scopes, IHostEnvironment environment, IConfiguration configuration, ILogger<FootballScheduler> logger)
    : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Quiet = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing") || !configuration.GetValue("Football:Scheduler:Enabled", true))
        {
            return;
        }
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        using var timer = new PeriodicTimer(Tick);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            if (await DueAsync(scope.ServiceProvider, ct))
            {
                await scope.ServiceProvider.GetRequiredService<IFootballSyncService>().SyncAsync(null, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Scheduled football sync failed");
        }
    }

    /// <summary>Due when never synced, quiet for 6 hours, a match is on (or about to start), or statistics are missing.</summary>
    private static async Task<bool> DueAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var last = await db.Set<Season>().Where(s => s.IsCurrent).MinAsync(s => s.LastSyncedAt, ct);
        if (last is null || now - last >= Quiet)
        {
            return true;
        }
        var from = now.AddMinutes(-130);
        var to = now.AddMinutes(10);
        return await db.Set<Fixture>().AnyAsync(f => f.Gameweek.Season.IsCurrent
            && ((f.KickoffAt >= from && f.KickoffAt <= to) || (f.Status == FixtureStatus.Finished && !f.PlayerStats.Any())), ct);
    }
}
