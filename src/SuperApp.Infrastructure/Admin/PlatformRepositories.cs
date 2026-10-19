using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.AdminConsole;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Admin;

/// <summary>ops.Settings (tracked, so the service can change values).</summary>
public sealed class PlatformSettingsRepository(AppDbContext db) : IPlatformSettingsRepository
{
    public async Task<IReadOnlyList<Setting>> ListAsync(string? group, CancellationToken ct) =>
        await db.Set<Setting>().Where(s => group == null || s.Group == group).OrderBy(s => s.Group).ThenBy(s => s.Key).ToListAsync(ct);
}

/// <summary>Broadcasts; delivery writes one bell notification per active account of the segment in a single statement.</summary>
public sealed class BroadcastRepository(AppDbContext db, AdminReader reader) : IBroadcastRepository
{
    public async Task<PagedResult<BroadcastDto>> ListAsync(int page, int pageSize, CancellationToken ct) =>
        await AdminReader.PageAsync(db.Set<Broadcast>().AsNoTracking().OrderByDescending(b => b.Id).Select(b => new BroadcastDto(
            b.Id, b.Title, b.Body, b.Link, b.Segment.ToString(), b.SendEmail, b.ScheduledAt, b.SentAt, b.RecipientsCount, b.CreatedAt)), page, pageSize, ct);

    public void Add(Broadcast broadcast) => db.Set<Broadcast>().Add(broadcast);

    public Task<Broadcast?> GetAsync(long id, CancellationToken ct) => db.Set<Broadcast>().FirstOrDefaultAsync(b => b.Id == id, ct);

    public async Task<IReadOnlyList<Broadcast>> DueAsync(DateTime now, CancellationToken ct) =>
        await db.Set<Broadcast>().Where(b => b.SentAt == null && b.ScheduledAt != null && b.ScheduledAt <= now).ToListAsync(ct);

    public async Task<int> DeliverAsync(Broadcast broadcast, CancellationToken ct)
    {
        var ids = await Recipients(broadcast.Segment).Distinct().ToListAsync(ct);
        db.Set<Notification>().AddRange(ids.Select(id => new Notification
        {
            UserId = id, Category = NotificationCategory.Account, Title = broadcast.Title, Body = broadcast.Body, Link = broadcast.Link,
            Tone = NotificationTone.Info, CreatedAt = DateTime.UtcNow,
        }));
        await db.SaveChangesAsync(ct);
        return ids.Count;
    }

    private IQueryable<Guid> Recipients(BroadcastSegment segment)
    {
        var active = db.Users.Where(u => u.Status != UserStatus.Suspended && u.AccountType == AccountType.Person);
        return segment switch
        {
            BroadcastSegment.Customers => reader.Customers().Where(u => u.Status != UserStatus.Suspended).Select(u => u.Id),
            BroadcastSegment.Partners => active.Where(u => db.Set<Partner>().Any(p => p.UserId == u.Id)).Select(u => u.Id),
            BroadcastSegment.Sellers => active.Where(u => db.Set<Seller>().Any(s => s.UserId == u.Id)).Select(u => u.Id),
            BroadcastSegment.Factories => active.Where(u => db.Set<Factory>().Any(f => f.OwnerUserId == u.Id)).Select(u => u.Id),
            BroadcastSegment.FantasyPlayers => active.Where(u => db.Set<FantasyTeam>().Any(t => t.UserId == u.Id)).Select(u => u.Id),
            _ => active.Select(u => u.Id),
        };
    }
}

/// <summary>
/// Sends scheduled broadcasts every minute. Off in the "Testing" environment and when <c>Broadcasts:Scheduler:Enabled</c> is
/// false. When Hangfire is wired up this becomes a recurring job calling <see cref="IPlatformService.SendDueAsync"/>.
/// </summary>
public sealed class BroadcastScheduler(IServiceScopeFactory scopes, IHostEnvironment environment, IConfiguration configuration, ILogger<BroadcastScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing") || !configuration.GetValue("Broadcasts:Scheduler:Enabled", true))
        {
            return;
        }
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IPlatformService>().SendDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Sending scheduled broadcasts failed");
            }
        }
    }
}
