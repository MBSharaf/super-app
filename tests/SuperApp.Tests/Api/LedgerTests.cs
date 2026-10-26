using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Money;
using SuperApp.Infrastructure.Payments;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Tests.Api;

/// <summary>Paid bookings feed the partner's held earnings and the platform; refunds go to the customer's wallet; holds are released.</summary>
[Trait("Category", "Database")]
public sealed class LedgerTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private static JsonElement Wallet(JsonElement wallets, string type) => wallets.EnumerateArray().Single(w => w.GetProperty("type").GetString() == type);

    [Fact]
    public async Task A_paid_booking_holds_the_partners_earning_and_a_cancel_refunds_the_wallet()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString();
        var (player, _) = await kit.PersonAsync(partner: false);
        var first = (await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(3), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true })).Body.Data();
        var total = first.GetProperty("total").GetDecimal();
        var fee = first.GetProperty("serviceFee").GetDecimal();

        var held = Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerBookings");
        held.GetProperty("pending").GetDecimal().ShouldBeInRange(0.01m, total - fee);
        held.GetProperty("available").GetDecimal().ShouldBe(0);
        var receipts = (await player.GetAsync("/api/v1/receipts")).Body.Data();
        var number = receipts[0].GetProperty("number").GetString()!;
        receipts[0].GetProperty("refCode").GetString().ShouldBe(first.GetProperty("code").GetString());
        var pdf = await player.Http.GetAsync($"/api/v1/receipts/{number}.pdf");
        pdf.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await pdf.Content.ReadAsByteArrayAsync()).Take(4).ShouldBe("%PDF"u8.ToArray());
        (await partner.Http.GetAsync($"/api/v1/receipts/{number}.pdf")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await player.PostAsync($"/api/v1/bookings/{first.GetProperty("code").GetString()}/cancel")).Status.ShouldBe(HttpStatusCode.OK);
        Wallet((await player.GetAsync("/api/v1/wallet")).Body.Data(), "Customer").GetProperty("available").GetDecimal().ShouldBe(total);
        Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerBookings").GetProperty("pending").GetDecimal().ShouldBe(0);
        (await player.GetAsync("/api/v1/receipts")).Body.Data().GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Held_earnings_become_available_after_the_hold()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var code = (await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(2), hour = 18, duration = 1, payAtVenue = false, acceptWaiver = true }))
            .Body.Data().GetProperty("code").GetString();
        var pending = Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerBookings").GetProperty("pending").GetDecimal();

        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var id = await db.Set<SuperApp.Domain.Entities.Bookings.Booking>().Where(b => b.Code == code).Select(b => b.Id).SingleAsync();
            await db.Set<LedgerEntry>().Where(e => e.RefType == "Booking" && e.RefId == id).ExecuteUpdateAsync(u => u.SetProperty(e => e.AvailableAt, e => e.AvailableAt == null ? null : DateTime.UtcNow.AddMinutes(-1)));
        }
        var job = api.Services.GetRequiredService<MoneyReleaseJob>();
        (await job.RunOnceAsync(CancellationToken.None)).ShouldBeGreaterThanOrEqualTo(1);
        await job.RunOnceAsync(CancellationToken.None);   // once only

        var wallet = Wallet((await partner.GetAsync("/api/v1/wallet")).Body.Data(), "PartnerBookings");
        wallet.GetProperty("available").GetDecimal().ShouldBe(pending);
        wallet.GetProperty("pending").GetDecimal().ShouldBe(0);
    }
}
