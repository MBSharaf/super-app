using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SuperApp.Infrastructure.Email;
using SuperApp.Infrastructure.Persistence;
using SuperApp.Infrastructure.Persistence.Seed;

namespace SuperApp.Tests.Api;

/// <summary>
/// The real API on a throw-away LocalDB database with the reference seed (no demo data) and a CEO account.
/// Emails are kept in memory so tests can open the links; the database is dropped at the end.
/// </summary>
public sealed class AccountsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string CeoEmail = "ceo@superapp.test";
    public const string CeoPassword = "Ceo-test-2026";
    private readonly string database = $"SuperApp_ApiTest_{Guid.NewGuid():N}";

    public CapturingEmailTransport Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", $"Server=(localdb)\\MSSQLLocalDB;Database={database};Trusted_Connection=True;TrustServerCertificate=True");
        builder.UseSetting("Security:Data_Protection_Keys_Path", Path.Combine(Path.GetTempPath(), "superapp-test-keys"));
        builder.UseSetting("Seed:Ceo_Email", CeoEmail);
        builder.UseSetting("Seed:Ceo_Password", CeoPassword);
        builder.UseSetting("Seed:DemoData", "false");
        builder.UseSetting("RateLimits:Auth", "1000");
        builder.UseSetting("RateLimits:Write", "1000");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IEmailTransport>(Emails);
            services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
        scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().RunAsync().GetAwaiter().GetResult();
        return host;
    }

    /// <summary>A browser-like client: https, its own cookies, and the CSRF header ready.</summary>
    public async Task<ApiClient> ClientAsync()
    {
        var http = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var client = new ApiClient(http);
        await client.RefreshCsrfAsync();
        return client;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public new async Task DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
        }
        await base.DisposeAsync();
    }
}

/// <summary>Keeps sent emails in memory; <see cref="LinkTo"/> reads the button link of the last email to an address.</summary>
public sealed partial class CapturingEmailTransport : IEmailTransport
{
    private readonly ConcurrentQueue<(string To, string Subject, string Html)> sent = new();

    public IReadOnlyList<(string To, string Subject, string Html)> Sent => [.. sent];

    public Task DeliverAsync(string to, string subject, string html, CancellationToken ct)
    {
        sent.Enqueue((to, subject, html));
        return Task.CompletedTask;
    }

    public Uri? LinkTo(string to, string page)
    {
        var mail = Sent.LastOrDefault(m => m.To == to && m.Html.Contains("#/" + page, StringComparison.Ordinal));
        var match = mail.Html is null ? null : Href().Match(mail.Html);
        return match is { Success: true } ? new Uri(WebUtility.HtmlDecode(match.Groups[1].Value)) : null;
    }

    [GeneratedRegex("href=\"([^\"]+)\"")]
    private static partial Regex Href();
}
