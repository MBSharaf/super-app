using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
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

    private readonly SemaphoreSlim ceoSetup = new(1, 1);

    public CapturingEmailTransport Emails { get; } = new();

    /// <summary>The CEO's authenticator key once 2FA is set up (by the first <see cref="CeoAsync"/>).</summary>
    public string? CeoKey { get; private set; }

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
        builder.UseSetting("RateLimits:Upload", "1000");
        builder.UseSetting("Storage:Local_Root", Path.Combine(Path.GetTempPath(), "superapp-test-storage", database));
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

    /// <summary>A CEO session that passed 2FA (sets the authenticator up the first time).</summary>
    public async Task<ApiClient> CeoAsync()
    {
        var client = await ClientAsync();
        var signIn = await client.SignInAsync(CeoEmail, CeoPassword);
        await ceoSetup.WaitAsync();
        try
        {
            if (signIn.GetProperty("data").GetProperty("requires2fa").GetBoolean())
            {
                (await client.PostAsync("/api/v1/auth/2fa/verify", new { code = Totp.Code(CeoKey!) })).Status.ShouldBe(HttpStatusCode.OK);
            }
            else
            {
                CeoKey = await client.EnableTwoFactorAsync();
            }
        }
        finally
        {
            ceoSetup.Release();
        }
        await client.RefreshCsrfAsync();
        return client;
    }

    /// <summary>Runs code against the test database (to check rows or flip a switch).</summary>
    public async Task<T> DbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        using var scope = Services.CreateScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
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
