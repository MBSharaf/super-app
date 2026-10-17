using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Email;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;
using SuperApp.Infrastructure.Persistence.Seed;

namespace SuperApp.Infrastructure.Email;

/// <summary>Where an email finally goes (a file in development, SMTP when the domain exists).</summary>
public interface IEmailTransport
{
    Task DeliverAsync(string to, string subject, string html, CancellationToken ct);
}

/// <summary>
/// Records every email in notify.EmailOutbox, then delivers it. A delivery failure is stored on the row
/// (status Failed + error) and logged — it never breaks the action that sent the email.
/// </summary>
public sealed class EmailService(AppDbContext db, IEmailTransport transport, IClock clock, ILogger<EmailService> logger) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var (subject, html) = EmailTemplates.Render(message.Template, message.Language, message.Model);
        var row = new OutboxEmail
        {
            UserId = message.UserId, ToAddress = message.To, Subject = subject, Template = message.Template,
            ModelJson = SeedFile.ToJson(message.Model), Language = message.Language, Status = EmailStatus.Queued,
        };
        db.Set<OutboxEmail>().Add(row);
        await db.SaveChangesAsync(ct);
        try
        {
            await transport.DeliverAsync(message.To, subject, html, ct);
            row.Status = EmailStatus.Sent;
            row.SentAt = clock.UtcNow;
        }
#pragma warning disable CA1031 // a failed email must not undo the action; it is recorded for a retry
        catch (Exception ex)
#pragma warning restore CA1031
        {
            row.Status = EmailStatus.Failed;
            row.Error = ex.Message.Length > 400 ? ex.Message[..400] : ex.Message;
            logger.LogError(ex, "Email {Template} to {To} failed", message.Template, message.To);
        }
        row.Attempts++;
        await db.SaveChangesAsync(ct);
    }
}
