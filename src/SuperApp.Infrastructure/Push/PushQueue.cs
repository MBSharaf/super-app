using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Push;

/// <summary>Notifications waiting to go to phones. Bounded: when Firebase is far behind, the oldest pushes are dropped (the bell still has them).</summary>
public sealed class PushQueue(FcmClient fcm) : IPushQueue
{
    private readonly Channel<(IReadOnlyList<Guid> Users, NotificationMessage Message)> channel =
        Channel.CreateBounded<(IReadOnlyList<Guid>, NotificationMessage)>(new BoundedChannelOptions(5000) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<(IReadOnlyList<Guid> Users, NotificationMessage Message)> Reader => channel.Reader;

    public void Enqueue(IReadOnlyList<Guid> userIds, NotificationMessage message)
    {
        if (fcm.IsOn && userIds.Count > 0)
        {
            channel.Writer.TryWrite((userIds, message));
        }
    }
}

/// <summary>Sends queued notifications to every phone of each person; forgets tokens Firebase says are gone.</summary>
public sealed class PushWorker(PushQueue queue, FcmClient fcm, IServiceScopeFactory scopes, ILogger<PushWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (users, message) in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await SendAsync(users, message, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Push notifications could not be sent");
            }
        }
    }

    private async Task SendAsync(IReadOnlyList<Guid> users, NotificationMessage message, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var chunk in users.Distinct().Chunk(500))
        {
            var devices = await db.Set<PushDevice>().Where(d => chunk.Contains(d.UserId)).ToListAsync(ct);
            var dead = new List<PushDevice>();
            foreach (var device in devices)
            {
                if (await fcm.SendAsync(device.Token, message.Title, message.Body, message.Link, ct) == PushResult.DeadToken)
                {
                    dead.Add(device);
                }
            }
            if (dead.Count > 0)
            {
                db.Set<PushDevice>().RemoveRange(dead);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
