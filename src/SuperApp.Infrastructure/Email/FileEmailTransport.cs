using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace SuperApp.Infrastructure.Email;

/// <summary>
/// Development sender (<c>EMAIL__MODE=File</c>): each email is saved as an .html file in the folder from
/// <c>EMAIL__FILE_FOLDER</c> (default "emails"), so links can be opened by hand until the domain exists.
/// </summary>
public sealed partial class FileEmailTransport(IConfiguration configuration) : IEmailTransport
{
    public async Task DeliverAsync(string to, string subject, string html, CancellationToken ct)
    {
        var folder = Path.GetFullPath(configuration["Email:File_Folder"] ?? "emails");
        Directory.CreateDirectory(folder);
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        var file = Path.Combine(folder, $"{stamp}-{Safe().Replace(to, "_")}.html");
        var header = $"<!-- To: {to} | Subject: {subject} -->\n";
        await File.WriteAllTextAsync(file, header + html, ct);
    }

    [GeneratedRegex("[^a-zA-Z0-9@._-]")]
    private static partial Regex Safe();
}
