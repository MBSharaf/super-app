using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Application.Features.Payments;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Infrastructure.Payments;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Tests.Api;

/// <summary>The API with Paymob "keys" set; Paymob itself is a fake handler that answers the intention call.</summary>
public sealed class PaymobApiFactory : AccountsApiFactory
{
    public const string Hmac = "test-hmac-secret";

    /// <summary>Every merchant ref sent to "Paymob" (the payment code + "." + time).</summary>
    public ConcurrentQueue<string> References { get; } = new();

    /// <summary>Every refund asked of "Paymob": (transaction id, amount in cents).</summary>
    public ConcurrentQueue<(string Transaction, long Cents)> Refunds { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Payments:Paymob:SecretKey", "test-secret");
        builder.UseSetting("Payments:Paymob:PublicKey", "test-public");
        builder.UseSetting("Payments:Paymob:HmacSecret", Hmac);
        builder.UseSetting("Payments:Paymob:IntegrationIds", "111,222");
        builder.ConfigureTestServices(services =>
            services.AddHttpClient<IPaymentGateway, PaymobGateway>().ConfigurePrimaryHttpMessageHandler(() => new FakePaymob(References, Refunds)));
    }

    private sealed class FakePaymob(ConcurrentQueue<string> references, ConcurrentQueue<(string, long)> refunds) : HttpMessageHandler
    {
        private static long orderId = 1_000_000;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization!.ToString().ShouldBe("Token test-secret");
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (request.RequestUri!.AbsolutePath.EndsWith("void_refund/refund", StringComparison.Ordinal))
            {
                refunds.Enqueue((body.RootElement.GetProperty("transaction_id").ToString(), body.RootElement.GetProperty("amount_cents").GetInt64()));
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"id":7770001,"success":true}""", Encoding.UTF8, "application/json") };
            }
            references.Enqueue(body.RootElement.GetProperty("special_reference").GetString()!);
            var json = $$"""{"id":"pi_test","client_secret":"egy_csk_test","intention_order_id":{{Interlocked.Increment(ref orderId)}}}""";
            return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}

[Trait("Category", "Database")]
public sealed class PaymentsTests(PaymobApiFactory api) : IClassFixture<PaymobApiFactory>
{
    private readonly PartnerKit kit = new(api);

    /// <summary>A Paymob "transaction processed" callback, signed the way Paymob signs it.</summary>
    private static (string Body, string Hmac) Callback(string merchantRef, long cents, bool success, string? secret = null, long transaction = 5550001)
    {
        var obj = new Dictionary<string, object?>
        {
            ["id"] = transaction, ["pending"] = false, ["amount_cents"] = cents, ["success"] = success, ["is_auth"] = false, ["is_capture"] = false,
            ["is_standalone_payment"] = true, ["is_voided"] = false, ["is_refunded"] = false, ["is_3d_secure"] = true, ["integration_id"] = 111,
            ["has_parent_transaction"] = false, ["owner"] = 42, ["created_at"] = "2026-10-26T18:04:12.000000", ["currency"] = "EGP",
            ["error_occured"] = false, ["order"] = new { id = 987654, merchant_order_id = merchantRef },
            ["source_data"] = new { pan = "2346", sub_type = "MasterCard", type = "card" },
        };
        var signed = $"{cents}2026-10-26T18:04:12.000000EGPfalsefalse{transaction}111truefalsefalsefalsetruefalse98765442false2346MasterCardcard{(success ? "true" : "false")}";
        var hmac = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(secret ?? PaymobApiFactory.Hmac), Encoding.UTF8.GetBytes(signed))).ToLowerInvariant();
        return (JsonSerializer.Serialize(new { type = "TRANSACTION", obj }), hmac);
    }

    private async Task<HttpStatusCode> PostCallbackAsync((string Body, string Hmac) callback)
    {
        var http = api.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var response = await http.PostAsync($"/api/v1/webhooks/payments/paymob?hmac={callback.Hmac}", new StringContent(callback.Body, Encoding.UTF8, "application/json"));
        return response.StatusCode;
    }

    [Fact]
    public async Task A_booking_waits_for_the_signed_paymob_callback_and_is_confirmed_once()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var made = await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(3), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true });
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var booking = made.Body.Data();
        booking.GetProperty("status").GetString().ShouldBe("AwaitingPayment");
        booking.GetProperty("paymentUrl").GetString()!.ShouldStartWith("https://accept.paymob.com/unifiedcheckout/?publicKey=test-public&clientSecret=egy_csk_test");
        var code = booking.GetProperty("code").GetString()!;
        var cents = (long)(booking.GetProperty("deposit").GetDecimal() * 100);
        var reference = api.References.Last();

        (await PostCallbackAsync(Callback(reference, cents, success: true, secret: "wrong"))).ShouldBe(HttpStatusCode.BadRequest);
        (await PostCallbackAsync(("not json {", "abc123"))).ShouldBe(HttpStatusCode.BadRequest);
        (await PostCallbackAsync(("{\"obj\":{}}", "abc123"))).ShouldBe(HttpStatusCode.BadRequest);
        (await PostCallbackAsync(Callback(reference, cents - 100, success: true))).ShouldBe(HttpStatusCode.OK);   // wrong amount → failed
        (await player.GetAsync($"/api/v1/bookings/{code}")).Body.Data().GetProperty("status").GetString().ShouldBe("AwaitingPayment");

        var payment = reference.Split('.')[0];
        (await player.PostAsync($"/api/v1/payments/{payment}/retry")).Body.Data().GetProperty("redirectUrl").GetString().ShouldNotBeNull();
        (await PostCallbackAsync(Callback(api.References.Last(), cents, success: true))).ShouldBe(HttpStatusCode.OK);
        (await PostCallbackAsync(Callback(api.References.Last(), cents, success: true))).ShouldBe(HttpStatusCode.OK);   // repeat: no change
        (await player.GetAsync($"/api/v1/bookings/{code}")).Body.Data().GetProperty("status").GetString().ShouldBe("Confirmed");
        (await player.GetAsync($"/api/v1/payments/{payment}")).Body.Data().GetProperty("status").GetString().ShouldBe("Paid");
        (await player.PostAsync($"/api/v1/payments/{payment}/retry")).Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var (stranger, _) = await kit.PersonAsync(partner: false);
        (await stranger.GetAsync($"/api/v1/payments/{payment}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unpaid_booking_is_cancelled_after_15_minutes_and_its_hour_freed()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString();
        var (player, _) = await kit.PersonAsync(partner: false);
        var request = new { pitchSlug = slug, date = PartnerKit.Day(4), hour = 19, duration = 1, payAtVenue = false, acceptWaiver = true };
        var code = (await player.PostAsync("/api/v1/bookings", request)).Body.Data().GetProperty("code").GetString()!;

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<Booking>().Where(b => b.Code == code).ExecuteUpdateAsync(u => u.SetProperty(b => b.CreatedAt, b => b.CreatedAt.AddMinutes(-16)));
        }
        (await api.Services.GetRequiredService<AwaitingPaymentExpiry>().RunOnceAsync(CancellationToken.None)).ShouldBeGreaterThanOrEqualTo(1);

        (await player.GetAsync($"/api/v1/bookings/{code}")).Body.Data().GetProperty("status").GetString().ShouldBe("CancelledByCustomer");
        var (other, _) = await kit.PersonAsync(partner: false);
        (await other.PostAsync("/api/v1/bookings", request)).Status.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_payment_that_arrives_after_the_booking_expired_is_refunded_to_the_card_at_once()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, playerId) = await kit.PersonAsync(partner: false);
        var request = new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(6), hour = 18, duration = 1, payAtVenue = false, acceptWaiver = true };
        var booking = (await player.PostAsync("/api/v1/bookings", request)).Body.Data();
        var code = booking.GetProperty("code").GetString()!;
        var cents = (long)(booking.GetProperty("deposit").GetDecimal() * 100);
        var reference = api.References.Last();
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<Booking>().Where(b => b.Code == code).ExecuteUpdateAsync(u => u.SetProperty(b => b.CreatedAt, b => b.CreatedAt.AddMinutes(-16)));
        }
        await api.Services.GetRequiredService<AwaitingPaymentExpiry>().RunOnceAsync(CancellationToken.None);

        // The customer paid on the gateway page after the 15 minutes: the money arrives for a booking that is gone
        (await PostCallbackAsync(Callback(reference, cents, success: true, transaction: 5550077))).ShouldBe(HttpStatusCode.OK);

        (await player.GetAsync($"/api/v1/bookings/{code}")).Body.Data().GetProperty("status").GetString().ShouldBe("CancelledByCustomer");
        var payment = reference.Split('.')[0];
        (await player.GetAsync($"/api/v1/payments/{payment}")).Body.Data().GetProperty("status").GetString().ShouldBe("Refunded");
        api.Refunds.ShouldContain(("5550077", cents));                       // back to the card, against the gateway's transaction
        var (refund, receipt, ledgerLines, notices) = await api.DbAsync(async db =>
        {
            var paymentId = await db.Set<SuperApp.Domain.Entities.Money.Payment>().Where(p => p.Code == payment).Select(p => p.Id).SingleAsync();
            var bookingId = await db.Set<Booking>().Where(b => b.Code == code).Select(b => b.Id).SingleAsync();
            return (
                await db.Set<SuperApp.Domain.Entities.Money.Refund>().SingleAsync(r => r.PaymentId == paymentId),
                await db.Set<SuperApp.Domain.Entities.Money.Receipt>().CountAsync(r => r.Kind == SuperApp.Domain.Enums.ReceiptKind.Refund && r.RefId == bookingId),
                await db.Set<SuperApp.Domain.Entities.Money.LedgerEntry>().CountAsync(l => l.RefType == "Booking" && l.RefId == bookingId),
                await db.Set<SuperApp.Domain.Entities.Notifications.Notification>().CountAsync(n => n.UserId == playerId && n.Title == "Late payment refunded"));
        });
        refund.Status.ShouldBe(SuperApp.Domain.Enums.RefundStatus.Done);
        refund.Amount.ShouldBe(cents / 100m);
        receipt.ShouldBe(1);
        ledgerLines.ShouldBe(0);                                             // nothing reached the venue or Super App
        notices.ShouldBe(1);
    }
}
