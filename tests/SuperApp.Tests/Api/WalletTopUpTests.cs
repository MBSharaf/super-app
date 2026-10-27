using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>Wallet top-ups (simulated gateway: paid at once) and paying a booking from the wallet.</summary>
[Trait("Category", "Database")]
public sealed class WalletTopUpTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private static async Task<(HttpStatusCode Status, JsonElement Body)> TopUpAsync(ApiClient client, decimal amount, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/wallet/top-ups") { Content = JsonContent.Create(new { amount }) };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }
        var response = await client.Http.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static async Task<decimal> BalanceAsync(ApiClient client) =>
        (await client.GetAsync("/api/v1/wallet")).Body.Data().EnumerateArray()
            .Where(w => w.GetProperty("type").GetString() == "Customer").Select(w => w.GetProperty("available").GetDecimal()).DefaultIfEmpty(0).Single();

    [Fact]
    public async Task A_top_up_lands_in_the_wallet_once_with_a_receipt()
    {
        var (player, _) = await kit.PersonAsync(partner: false);
        (await TopUpAsync(player, 500, null)).Body.Code().ShouldBe("idempotency_key");
        (await TopUpAsync(player, 20, "k0")).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await TopUpAsync(player, 100.5m, "k0")).Status.ShouldBe(HttpStatusCode.BadRequest);

        var key = Guid.NewGuid().ToString("N");
        var first = await TopUpAsync(player, 500, key);
        first.Status.ShouldBe(HttpStatusCode.OK, first.Body.ToString());
        (first.Body.Data().GetProperty("status").GetString(), first.Body.Data().GetProperty("balance").GetDecimal()).ShouldBe(("Paid", 500m));
        var again = await TopUpAsync(player, 500, key);
        again.Body.Data().GetProperty("code").GetString().ShouldBe(first.Body.Data().GetProperty("code").GetString());
        (await BalanceAsync(player)).ShouldBe(500);

        var receipt = (await player.GetAsync("/api/v1/receipts")).Body.Data().EnumerateArray().Single();
        (receipt.GetProperty("kind").GetString(), receipt.GetProperty("refCode").GetString()).ShouldBe(("TopUp", first.Body.Data().GetProperty("code").GetString()));
        var pdf = await player.Http.GetAsync($"/api/v1/receipts/{receipt.GetProperty("number").GetString()}.pdf");
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ledger = (await player.GetAsync("/api/v1/wallet/ledger?type=Customer")).Body.Data();
        ledger.EnumerateArray().Single().GetProperty("kind").GetString().ShouldBe("WalletTopUp");
    }

    [Fact]
    public async Task A_booking_paid_from_the_wallet_takes_the_money_and_a_cancel_gives_it_back()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var booking = new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(4), hour = 19, duration = 1, payAtVenue = false, acceptWaiver = true, payFromWallet = true };
        (await player.PostAsync("/api/v1/bookings", booking)).Body.Code().ShouldBe("insufficient_funds");

        (await TopUpAsync(player, 2000, Guid.NewGuid().ToString("N"))).Status.ShouldBe(HttpStatusCode.OK);
        var made = await player.PostAsync("/api/v1/bookings", booking);
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var total = made.Body.Data().GetProperty("total").GetDecimal();
        made.Body.Data().GetProperty("status").GetString().ShouldBe("Confirmed");
        (await BalanceAsync(player)).ShouldBe(2000 - total);

        (await player.PostAsync($"/api/v1/bookings/{made.Body.Data().GetProperty("code").GetString()}/cancel")).Status.ShouldBe(HttpStatusCode.OK);
        (await BalanceAsync(player)).ShouldBe(2000);
    }
}
