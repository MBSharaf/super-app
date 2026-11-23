using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Features.Webhooks;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Automation;

/// <summary>
/// The owner's n8n links (settings <c>Automation:N8n:Orders|Bookings|Dashboard|Support</c>, the same links as the demo site's
/// SA.config.automation) become webhook subscriptions named "n8n · …" that get the demo site's JSON
/// (<see cref="WebhookEvents.SiteFormat"/>). Checked at start-up: a new link is added, a changed link is updated; the CEO can
/// still pause one in CEO → Webhooks (that choice is kept). An empty setting leaves the subscription as it is.
/// </summary>
public sealed class N8nLinks(IServiceScopeFactory scopes, IConfiguration configuration, IHostEnvironment environment, ILogger<N8nLinks> logger) : IHostedService
{
    /// <summary>Link → the server events it gets (what the demo site sends to the same link).</summary>
    public static readonly IReadOnlyDictionary<string, string[]> Routes = new Dictionary<string, string[]>
    {
        ["Orders"] = [WebhookEvents.OrderCreated],
        ["Bookings"] = [WebhookEvents.BookingCreated, WebhookEvents.BookingCancelled, WebhookEvents.PackageCreated],
        ["Dashboard"] = [WebhookEvents.PitchSubmitted, WebhookEvents.PitchDeleted, WebhookEvents.ReviewCreated, WebhookEvents.SlotPosted],
        ["Support"] = [WebhookEvents.CancellationRequested, WebhookEvents.CancellationApproved, WebhookEvents.CancellationRejected],
    };

    public static string NameOf(string key) => $"n8n · {key.ToLowerInvariant()}";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }
        try
        {
            await SyncAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The n8n links could not be checked; the site carries on without them.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Adds or updates one subscription per link that is set. Returns how many changed.</summary>
    public async Task<int> SyncAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IWebhookSender>();
        var changed = 0;
        foreach (var (key, events) in Routes)
        {
            var url = configuration[$"Automation:N8n:{key}"]?.Trim();
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            {
                continue;
            }
            var json = JsonSerializer.Serialize(events.Append(WebhookEvents.SiteFormat));
            var name = NameOf(key);
            var sub = await db.Set<WebhookSubscription>().FirstOrDefaultAsync(w => w.Name == name, ct);
            if (sub is null)
            {
                db.Set<WebhookSubscription>().Add(new WebhookSubscription { Name = name, Url = url, Events = json, SecretHash = sender.NewSecret().Salt });
                changed++;
            }
            else if (sub.Url != url || sub.Events != json)
            {
                (sub.Url, sub.Events) = (url, json);
                changed++;
            }
        }
        await db.SaveChangesAsync(ct);
        return changed;
    }
}
