namespace SuperApp.Application.Abstractions.Email;

/// <summary>One email to send: a template name and the values it shows.</summary>
public sealed record EmailMessage(string To, string Template, IReadOnlyDictionary<string, string> Model, string Language, Guid? UserId = null);

/// <summary>
/// Sends email through the outbox: every message is recorded in notify.EmailOutbox, then delivered by the active
/// sender (files in development, SMTP later). Templates: confirm-email, reset-password, admin-invite…
/// </summary>
public interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
