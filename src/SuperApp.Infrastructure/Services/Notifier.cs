using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Services;

/// <summary>Writes notifications to notify.Notifications (the site's bell). Live push (SignalR) comes with Phase 6.</summary>
public sealed class Notifier(AppDbContext db) : INotifier
{
    private const int BatchSize = 1000;

    public async Task NotifyUserAsync(Guid userId, NotificationMessage message, CancellationToken ct)
    {
        db.Set<Notification>().Add(ToRow(userId, message));
        await db.SaveChangesAsync(ct);
    }

    public async Task NotifyManyAsync(IReadOnlyList<(Guid UserId, NotificationMessage Message)> items, CancellationToken ct)
    {
        foreach (var batch in items.Chunk(BatchSize))
        {
            var rows = batch.Select(i => ToRow(i.UserId, i.Message)).ToList();
            db.Set<Notification>().AddRange(rows);
            await db.SaveChangesAsync(ct);
            rows.ForEach(r => db.Entry(r).State = EntityState.Detached);   // keeps memory flat for large audiences
        }
    }

    public async Task NotifyStaffAsync(string permission, NotificationMessage message, CancellationToken ct)
    {
        var people = await StaffWithAsync(permission).ToListAsync(ct);
        db.Set<Notification>().AddRange(people.Select(id => ToRow(id, message)));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Active people (not agents) holding the permission through a role or a direct grant, plus the CEO.</summary>
    private IQueryable<Guid> StaffWithAsync(string permission) =>
        db.Users.Where(u => u.Status == UserStatus.Active && u.AccountType == AccountType.Person)
            .Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && (ur.Role.Name == SystemRoles.Ceo
                    || db.Set<RolePermission>().Any(rp => rp.RoleId == ur.RoleId && rp.Permission.Code == permission)))
                || db.Set<UserPermission>().Any(up => up.UserId == u.Id && up.Permission.Code == permission))
            .Select(u => u.Id);

    private static Notification ToRow(Guid userId, NotificationMessage m) => new()
    {
        UserId = userId,
        Category = m.Category,
        Title = m.Title.Length > 140 ? m.Title[..140] : m.Title,
        Body = m.Body,
        Link = m.Link,
        Tone = m.Tone,
    };
}
