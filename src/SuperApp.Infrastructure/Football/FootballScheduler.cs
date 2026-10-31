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
/// Runs the football sync per competition: every 6 hours; a fantasy competition every 10 minutes while one of its
/// matches is on or a finished match still waits for its statistics; the others every 30 minutes while their matches
/// are on (the free sources have no live minute-by-minute data). Off in the "Testing" environment and when
/// <c>Football:Scheduler:Enabled</c> is false.
/// </summary>
public sealed class FootballScheduler(IServiceScopeFactory scopes, IHostEnvironment environment, IConfiguration configuration, ILogger<FootballScheduler> logger)
    : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Quiet = TimeSpan.FromHours(6);
    private static readonly TimeSpan OthersWhilePlaying = TimeSpan.FromMinutes(30);

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
            var sync = scope.ServiceProvider.GetRequiredService<IFootballSyncService>();
            foreach (var competition in await DueAsync(scope.ServiceProvider, ct))
            {
                await sync.SyncAsync(competition, ct);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Scheduled football sync failed");
        }
    }

    /// <summary>The competitions to sync now (see the class summary).</summary>
    private static async Task<IReadOnlyList<string>> DueAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var (from, to) = (now.AddMinutes(-130), now.AddMinutes(10));
        var rows = await db.Set<Competition>().AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).Select(c => new
        {
            c.Key,
            c.FantasyEnabled,
            Last = db.Set<Season>().Where(s => s.CompetitionId == c.Id && s.IsCurrent).Select(s => s.LastSyncedAt).FirstOrDefault(),
            Playing = db.Set<Fixture>().Any(f => f.Gameweek.Season.CompetitionId == c.Id && f.Gameweek.Season.IsCurrent && f.KickoffAt >= from && f.KickoffAt <= to),
            WaitingStats = c.FantasyEnabled && db.Set<Fixture>().Any(f => f.Gameweek.Season.CompetitionId == c.Id && f.Gameweek.Season.IsCurrent
                && f.Status == FixtureStatus.Finished && !f.PlayerStats.Any()),
        }).ToListAsync(ct);
        return rows.Where(r => r.Last is null || now - r.Last >= Quiet
                || (r.FantasyEnabled && (r.Playing || r.WaitingStats))
                || (!r.FantasyEnabled && r.Playing && now - r.Last >= OthersWhilePlaying))
            .Select(r => r.Key).ToList();
    }
}
