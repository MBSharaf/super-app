using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Webhooks;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Webhooks;

public sealed class WebhookStore(AppDbContext db) : IWebhookStore
{
    public async Task<IReadOnlyList<WebhookDto>> ListAsync(CancellationToken ct) =>
        (await db.Set<WebhookSubscription>().OrderBy(w => w.Id).ToListAsync(ct)).Select(ToDto).ToList();

    public Task<WebhookSubscription?> FindAsync(int id, CancellationToken ct) => db.Set<WebhookSubscription>().FirstOrDefaultAsync(w => w.Id == id, ct);

    public void Add(WebhookSubscription subscription) => db.Set<WebhookSubscription>().Add(subscription);

    public void Remove(WebhookSubscription subscription) => db.Set<WebhookSubscription>().Remove(subscription);

    public async Task<IReadOnlyList<WebhookDeliveryDto>> DeliveriesAsync(int id, CancellationToken ct) =>
        await db.Set<WebhookDelivery>().Where(d => d.SubscriptionId == id).OrderByDescending(d => d.At).Take(100)
            .Select(d => new WebhookDeliveryDto(d.Id, d.OutboxEvent.Type, d.OutboxEvent.EventId, d.Attempt, d.StatusCode, d.DurationMs, d.Error, d.At))
            .ToListAsync(ct);

    private static WebhookDto ToDto(WebhookSubscription w) => new(w.Id, w.Name, w.Url, WebhookSender.EventsOf(w), w.IsEnabled, w.FailureCount, w.CreatedAt);
}

/// <summary>
/// Sends one event as JSON: <c>{ id, type, occurredAt, data }</c>. Headers: X-SuperApp-Event, X-SuperApp-Delivery (event id,
/// the same on every retry), X-SuperApp-Timestamp (unix seconds) and X-SuperApp-Signature = "sha256=" + hex(HMAC-SHA256(secret,
/// timestamp + "." + body)). Receivers check the signature and ignore a delivery id they already handled.
/// </summary>
public sealed class WebhookSender(IHttpClientFactory http, IConfiguration configuration, IClock clock) : IWebhookSender
{
    /// <summary>
    /// Secrets are never stored: each one is HMAC(server key, salt). The salt is in the database; the server key is
    /// Webhooks:SigningKey (base64), else a file next to the data-protection keys, made on first use.
    /// </summary>
    private byte[] ServerKey()
    {
        if (configuration["Webhooks:SigningKey"] is { Length: > 0 } fromConfig)
        {
            return Convert.FromBase64String(fromConfig);
        }
        var folder = configuration["Security:Data_Protection_Keys_Path"] ?? "keys";
        var file = Path.Combine(folder, "webhooks.key");
        lock (typeof(WebhookSender))
        {
            if (!File.Exists(file))
            {
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(file, RandomNumberGenerator.GetBytes(32));
            }
            return File.ReadAllBytes(file);
        }
    }

    private string SecretOf(byte[] salt) => "whsec_" + Convert.ToHexString(HMACSHA256.HashData(ServerKey(), salt)).ToLowerInvariant()[..48];

    public (byte[] Salt, string Secret) NewSecret()
    {
        var salt = RandomNumberGenerator.GetBytes(32);
        return (salt, SecretOf(salt));
    }

    public static IReadOnlyList<string> EventsOf(WebhookSubscription w) =>
        System.Text.Json.JsonSerializer.Deserialize<List<string>>(w.Events) ?? [];

    public async Task<WebhookTestDto> SendAsync(WebhookSubscription subscription, Guid eventId, string type, DateTime occurredAt, string payloadJson, CancellationToken ct)
    {
        var body = $"{{\"id\":\"{eventId}\",\"type\":\"{type}\",\"occurredAt\":\"{occurredAt.ToString("o", CultureInfo.InvariantCulture)}\",\"data\":{payloadJson}}}";
        var stamp = new DateTimeOffset(clock.UtcNow).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var secret = Encoding.UTF8.GetBytes(SecretOf(subscription.SecretHash));
        var signature = Convert.ToHexString(HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes($"{stamp}.{body}"))).ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-SuperApp-Event", type);
        request.Headers.Add("X-SuperApp-Delivery", eventId.ToString());
        request.Headers.Add("X-SuperApp-Timestamp", stamp);
        request.Headers.Add("X-SuperApp-Signature", $"sha256={signature}");
        var watch = Stopwatch.StartNew();
        try
        {
            using var response = await http.CreateClient("webhooks").SendAsync(request, ct);
            return new WebhookTestDto(response.IsSuccessStatusCode, (int)response.StatusCode, (int)watch.ElapsedMilliseconds, response.IsSuccessStatusCode ? null : response.ReasonPhrase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new WebhookTestDto(false, null, (int)watch.ElapsedMilliseconds, ex.Message.Length > 300 ? ex.Message[..300] : ex.Message);
        }
    }
}

/// <summary>
/// Every 5 seconds, sends waiting events to each enabled webhook that follows them. A failed webhook is tried again after
/// 1 minute, 5 minutes, 30 minutes, 2 hours and 6 hours, then the event is marked failed; a webhook that already received an
/// event is not sent it again. Off in the "Testing" environment (tests call <see cref="RunOnceAsync"/>, which returns how many
/// events it handled).
/// </summary>
public sealed class WebhookDispatcher(IServiceScopeFactory scopes, IHostEnvironment environment, ILogger<WebhookDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Webhook dispatch failed; trying again shortly.");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IWebhookSender>();
        var now = scope.ServiceProvider.GetRequiredService<IClock>().UtcNow;
        var events = await db.Set<OutboxEvent>().Where(e => e.Status == OutboxStatus.Pending && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .OrderBy(e => e.Id).Take(20).ToListAsync(ct);
        if (events.Count == 0)
        {
            return 0;
        }
        var subs = await db.Set<WebhookSubscription>().Where(w => w.IsEnabled).ToListAsync(ct);
        foreach (var e in events)
        {
            await DeliverAsync(db, sender, subs, e, now, ct);
        }
        await db.SaveChangesAsync(ct);
        return events.Count;
    }

    private static async Task DeliverAsync(AppDbContext db, IWebhookSender sender, List<WebhookSubscription> subs, OutboxEvent e, DateTime now, CancellationToken ct)
    {
        var targets = subs.Where(s => WebhookSender.EventsOf(s).Any(x => x == "*" || x == e.Type)).ToList();
        var done = await db.Set<WebhookDelivery>().Where(d => d.OutboxEventId == e.Id && d.StatusCode >= 200 && d.StatusCode < 300).Select(d => d.SubscriptionId).ToListAsync(ct);
        var failed = false;
        foreach (var sub in targets.Where(s => !done.Contains(s.Id)))
        {
            var r = await sender.SendAsync(sub, e.EventId, e.Type, e.OccurredAt, e.PayloadJson, ct);
            db.Set<WebhookDelivery>().Add(new WebhookDelivery { SubscriptionId = sub.Id, OutboxEventId = e.Id, Attempt = (byte)(e.Attempts + 1),
                StatusCode = (short?)r.StatusCode, DurationMs = r.DurationMs, Error = r.Error, At = now });
            failed |= !r.Ok;
            sub.FailureCount = r.Ok ? 0 : sub.FailureCount + 1;
        }
        e.Attempts++;
        e.Status = !failed ? (targets.Count == 0 ? OutboxStatus.Skipped : OutboxStatus.Delivered) : e.Attempts > Backoff.Length ? OutboxStatus.Failed : OutboxStatus.Pending;
        e.NextAttemptAt = e.Status == OutboxStatus.Pending ? now + Backoff[e.Attempts - 1] : null;
    }
}

public static class WebhookRegistration
{
    public static IServiceCollection AddWebhooks(this IServiceCollection services)
    {
        services.AddHttpClient("webhooks", c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddScoped<IWebhookStore, WebhookStore>();
        services.AddScoped<IWebhookSender, WebhookSender>();
        services.AddSingleton<WebhookDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<WebhookDispatcher>());
        return services;
    }
}
