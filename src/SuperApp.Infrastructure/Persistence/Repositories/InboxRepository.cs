using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Inbox;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>The bell (notify.Notifications) and the phone switches (notify.NotificationPreferences.Push).</summary>
public sealed class InboxRepository(AppDbContext db) : IInboxRepository
{
    public async Task<InboxDto> LatestAsync(Guid userId, int take, CancellationToken ct)
    {
        var rows = await db.Set<Notification>().AsNoTracking().Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.Id).Take(take)
            .Select(n => new { n.Id, n.Category, n.Title, n.Body, n.Link, n.Tone, n.CreatedAt, n.ReadAt })
            .ToListAsync(ct);
        var unread = await db.Set<Notification>().CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);
        return new InboxDto(rows.Select(n => new InboxItemDto(n.Id, InboxService.Name(n.Category), n.Title, n.Body, n.Link,
            n.Tone.ToString().ToLowerInvariant(), n.CreatedAt, n.ReadAt is not null)).ToList(), unread);
    }

    public Task MarkReadAsync(Guid userId, IReadOnlyList<long>? ids, DateTime now, CancellationToken ct)
    {
        var unread = db.Set<Notification>().Where(n => n.UserId == userId && n.ReadAt == null);
        if (ids is { Count: > 0 })
        {
            var list = ids.ToList();
            unread = unread.Where(n => list.Contains(n.Id));
        }
        return unread.ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task<IReadOnlyList<NotificationCategory>> PushOffAsync(Guid userId, CancellationToken ct) =>
        await db.Set<NotificationPreference>().AsNoTracking().Where(p => p.UserId == userId && !p.Push).Select(p => p.Category).ToListAsync(ct);

    public async Task SetPushAsync(Guid userId, IReadOnlyDictionary<NotificationCategory, bool> push, CancellationToken ct)
    {
        var keys = push.Keys.ToList();
        var rows = await db.Set<NotificationPreference>().Where(p => p.UserId == userId && keys.Contains(p.Category)).ToListAsync(ct);
        foreach (var (category, on) in push)
        {
            var row = rows.FirstOrDefault(r => r.Category == category);
            if (row is null)
            {
                db.Set<NotificationPreference>().Add(new NotificationPreference { UserId = userId, Category = category, InApp = true, Email = true, Push = on });
            }
            else
            {
                row.Push = on;
            }
        }
    }
}
