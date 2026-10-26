using SuperApp.Domain.Enums;

namespace SuperApp.Application.Abstractions.Notifications;

/// <summary>One notification in a person's bell (notify.Notifications). Link is a site address starting with "#/".</summary>
public sealed record NotificationMessage(NotificationCategory Category, string Title, string? Body, string? Link, NotificationTone Tone = NotificationTone.Info);

/// <summary>"Every action notifies everyone concerned": the person it is about, and the team who must act.</summary>
public interface INotifier
{
    Task NotifyUserAsync(Guid userId, NotificationMessage message, CancellationToken ct);

    /// <summary>Many people at once (a gameweek's managers, a pitch's followers): saved in batches, not one save each.</summary>
    Task NotifyManyAsync(IReadOnlyList<(Guid UserId, NotificationMessage Message)> items, CancellationToken ct);

    /// <summary>Notifies every active team member (and the CEO) who holds the permission.</summary>
    Task NotifyStaffAsync(string permission, NotificationMessage message, CancellationToken ct);
}
