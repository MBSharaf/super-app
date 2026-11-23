using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SuperApp.Infrastructure.Automation;
using Shouldly;
using SuperApp.Infrastructure.Webhooks;

namespace SuperApp.Tests.Api;

/// <summary>Webhooks for n8n: the CEO adds one, events are signed and delivered, and a test ping works.</summary>
[Trait("Category", "Database")]
public sealed class WebhooksTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    /// <summary>A tiny receiver standing in for n8n: keeps every request's headers and body.</summary>
    private sealed class Receiver : IDisposable
    {
        private readonly HttpListener listener = new();
        public ConcurrentQueue<(string Event, string Stamp, string Signature, string Body)> Got { get; } = new();
        public string Url { get; }

        public Receiver()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            Url = $"http://localhost:{port}/hook/";
            listener.Prefixes.Add(Url);
            listener.Start();
            _ = Task.Run(LoopAsync);
        }

        private async Task LoopAsync()
        {
            while (listener.IsListening)
            {
                var ctx = await listener.GetContextAsync();
                using var reader = new StreamReader(ctx.Request.InputStream);
                Got.Enqueue((ctx.Request.Headers["X-SuperApp-Event"]!, ctx.Request.Headers["X-SuperApp-Timestamp"]!, ctx.Request.Headers["X-SuperApp-Signature"]!, await reader.ReadToEndAsync()));
                ctx.Response.StatusCode = 200;
                ctx.Response.Close();
            }
        }

        public void Dispose() => listener.Close();
    }

    [Fact]
    public async Task Events_are_signed_and_delivered_to_the_webhook()
    {
        using var n8n = new Receiver();
        var ceo = await api.CeoAsync();
        (await ceo.PostAsync("/api/v1/ceo/webhooks", new { name = "n8n", url = n8n.Url, events = new[] { "booking.nope" } })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var created = await ceo.PostAsync("/api/v1/ceo/webhooks", new { name = "n8n", url = n8n.Url, events = new[] { "booking.created", "booking.cancelled" } });
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var secret = created.Body.Data().GetProperty("secret").GetString()!;
        var id = created.Body.Data().GetProperty("webhook").GetProperty("id").GetInt32();

        (await ceo.PostAsync($"/api/v1/ceo/webhooks/{id}/test")).Body.Data().GetProperty("ok").GetBoolean().ShouldBeTrue();

        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(3), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true });
        var dispatcher = api.Services.GetRequiredService<WebhookDispatcher>();
        while (await dispatcher.RunOnceAsync(CancellationToken.None) > 0)
        {
            // 20 events per run: earlier events (the partner's set-up) may come before the booking
        }

        var booking = n8n.Got.ToArray().Single(g => g.Event == "booking.created");
        booking.Body.ShouldContain("\"type\":\"booking.created\"");
        var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{booking.Stamp}.{booking.Body}"))).ToLowerInvariant();
        booking.Signature.ShouldBe(expected);
        (await ceo.GetAsync($"/api/v1/ceo/webhooks/{id}/deliveries")).Body.Data().GetArrayLength().ShouldBeGreaterThan(0);
        (await player.GetAsync("/api/v1/ceo/webhooks")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// The owner's n8n links get the demo site's JSON (owner, 2026-11-23): the same event names and fields as
    /// js/services/automation.js, so one n8n workflow serves the GitHub site and supermalaab.com.
    /// </summary>
    [Fact]
    public async Task N8n_links_get_the_demo_sites_format()
    {
        using var n8n = new Receiver();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Automation:N8n:Bookings"] = n8n.Url }).Build();
        var links = new N8nLinks(api.Services.GetRequiredService<IServiceScopeFactory>(), config, api.Services.GetRequiredService<IHostEnvironment>(), NullLogger<N8nLinks>.Instance);
        (await links.SyncAsync(CancellationToken.None)).ShouldBe(1);
        (await links.SyncAsync(CancellationToken.None)).ShouldBe(0);   // already there: nothing changes
        var ceo = await api.CeoAsync();
        var hooks = (await ceo.GetAsync("/api/v1/ceo/webhooks")).Body.Data();
        var hook = hooks.EnumerateArray().Single(h => h.GetProperty("name").GetString() == "n8n · bookings");
        hook.GetProperty("events").EnumerateArray().Select(e => e.GetString()).ShouldContain("format:site");

        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var made = await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(4), hour = 19, duration = 1, payAtVenue = false, acceptWaiver = true });
        var dispatcher = api.Services.GetRequiredService<WebhookDispatcher>();
        while (await dispatcher.RunOnceAsync(CancellationToken.None) > 0)
        {
        }

        var got = n8n.Got.ToArray().Where(g => g.Event == "booking.created").Select(g => JsonDocument.Parse(g.Body).RootElement).ToList();
        got.ShouldNotBeEmpty();
        var body = got.Last();
        body.GetProperty("event").GetString().ShouldBe("booking.created");
        body.GetProperty("source").GetString().ShouldBe("server");
        body.TryGetProperty("type", out _).ShouldBeFalse();
        var booking = body.GetProperty("booking");
        booking.GetProperty("startTime").GetString().ShouldBe("19:00");
        booking.GetProperty("currency").GetString().ShouldBe("EGP");
        booking.GetProperty("link").GetString()!.ShouldContain("/booking/");
        body.GetProperty("pitch").GetProperty("name").GetString().ShouldBe(pitch.GetProperty("name").GetString());
        body.GetProperty("partner").GetProperty("email").GetString().ShouldNotBeNullOrEmpty();
        body.GetProperty("customer").GetProperty("email").GetString().ShouldNotBeNullOrEmpty();
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
    }

    /// <summary>Every event the demo site knows is built from the database: a booking, a free hour, a cancellation request and its approval.</summary>
    [Fact]
    public async Task Every_site_event_is_built_with_its_details()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var slug = pitch.GetProperty("slug").GetString()!;
        (await player.PostAsync("/api/v1/alerts", new { pitchSlug = slug })).Status.ShouldBe(HttpStatusCode.Created);
        (await partner.PostAsync("/api/v1/partner/free-slots", new { pitchId = pitch.GetProperty("id").GetInt64(), date = PartnerKit.Day(2), hour = 20, discountPercent = 30m }))
            .Status.ShouldBe(HttpStatusCode.Created);
        var made = await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(5), hour = 18, duration = 1, payAtVenue = false, acceptWaiver = true });
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        await api.CancelThroughRequestAsync(player, "booking", made.Body.Data().GetProperty("code").GetString()!);

        var events = await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Ops.OutboxEvent>().ToListAsync());
        using var scope = api.Services.CreateScope();
        var payloads = scope.ServiceProvider.GetRequiredService<SitePayloads>();
        var seen = new HashSet<string>();
        foreach (var e in events.Where(e => SitePayloads.Names.ContainsKey(e.Type)))
        {
            var json = await payloads.BuildAsync(e, CancellationToken.None);
            json.ShouldNotBeNull($"{e.Type} {e.PayloadJson}");
            var body = JsonDocument.Parse(json).RootElement;
            body.GetProperty("event").GetString().ShouldBe(SitePayloads.Names[e.Type]);
            seen.Add(body.GetProperty("event").GetString()!);
            if (e.Type == "slot.posted")
            {
                body.GetProperty("slot").GetProperty("discountPercent").GetDecimal().ShouldBe(30m);
                body.GetProperty("audience").GetProperty("recipients").GetArrayLength().ShouldBeGreaterThan(0);
            }
            if (e.Type == "cancellation.approved")
            {
                body.GetProperty("cancellation").GetProperty("decision").GetProperty("note").GetString().ShouldBe("OK");
                body.GetProperty("pitch").GetProperty("id").GetString().ShouldNotBeNullOrEmpty();
            }
        }
        seen.ShouldContain("booking.created");
        seen.ShouldContain("slot.posted");
        seen.ShouldContain("pitch.added");
        seen.ShouldContain("cancellation.requested");
        seen.ShouldContain("cancellation.approved");
    }
}
