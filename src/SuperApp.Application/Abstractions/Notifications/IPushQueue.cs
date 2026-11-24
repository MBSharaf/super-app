namespace SuperApp.Application.Abstractions.Notifications;

/// <summary>
/// Phone push for notifications that were just saved to the bell. Enqueue never waits for Firebase: a background worker
/// sends them, so a slow or switched-off push service never slows down or breaks the action itself.
/// </summary>
public interface IPushQueue
{
    void Enqueue(IReadOnlyList<Guid> userIds, NotificationMessage message);
}
