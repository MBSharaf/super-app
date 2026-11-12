using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace SuperApp.Infrastructure.Email;

/// <summary>
/// Real sender (<c>EMAIL__MODE=Smtp</c>): any SMTP provider (Brevo, Mailgun, Amazon SES, Google Workspace…) with the
/// settings <c>EMAIL__SMTP_HOST</c>, <c>__SMTP_PORT</c> (587 STARTTLS, 465 TLS), <c>__SMTP_USER</c> and <c>__SMTP_PASSWORD</c>.
/// The connection is always encrypted; a server that offers no TLS is refused.
/// </summary>
public sealed class SmtpEmailTransport(IConfiguration configuration) : IEmailTransport
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(20);

    public async Task DeliverAsync(string to, string subject, string html, CancellationToken ct)
    {
        var email = configuration.GetSection("Email");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(email["From_Name"] ?? "Super App", email["From"] ?? throw Missing("EMAIL__FROM")));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = html }.ToMessageBody();

        var port = int.TryParse(email["Smtp_Port"], out var p) ? p : 587;
        using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
        await client.ConnectAsync(email["Smtp_Host"] ?? throw Missing("EMAIL__SMTP_HOST"), port,
            port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        if (email["Smtp_User"] is { Length: > 0 } user)
        {
            await client.AuthenticateAsync(user, email["Smtp_Password"] ?? string.Empty, ct);
        }
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    /// <summary>True when .env asks for real email.</summary>
    public static bool IsOn(IConfiguration configuration) =>
        string.Equals(configuration["Email:Mode"], "Smtp", StringComparison.OrdinalIgnoreCase);

    private static InvalidOperationException Missing(string name) => new($"{name} is missing in .env (email is set to Smtp).");
}
