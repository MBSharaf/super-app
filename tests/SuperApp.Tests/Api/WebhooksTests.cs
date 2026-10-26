using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
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
        await api.Services.GetRequiredService<WebhookDispatcher>().RunOnceAsync(CancellationToken.None);

        var booking = n8n.Got.ToArray().Single(g => g.Event == "booking.created");
        booking.Body.ShouldContain("\"type\":\"booking.created\"");
        var expected = "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{booking.Stamp}.{booking.Body}"))).ToLowerInvariant();
        booking.Signature.ShouldBe(expected);
        (await ceo.GetAsync($"/api/v1/ceo/webhooks/{id}/deliveries")).Body.Data().GetArrayLength().ShouldBeGreaterThan(0);
        (await player.GetAsync("/api/v1/ceo/webhooks")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
