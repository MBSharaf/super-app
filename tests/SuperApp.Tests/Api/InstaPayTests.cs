using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Infrastructure.Payments;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Tests.Api;

/// <summary>The API with the owner's InstaPay account set and no Paymob keys: payments wait for the team's confirmation.</summary>
public sealed class InstaPayApiFactory : AccountsApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Payments:InstaPay:Address", "owner@instapay");
        builder.UseSetting("Payments:InstaPay:Link", "https://ipn.eg/C/Q/owner/instapay?ISIGN=test");
    }
}

[Trait("Category", "Database")]
public sealed class InstaPayTests(InstaPayApiFactory api) : IClassFixture<InstaPayApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private async Task<(ApiClient Player, string Booking, string Payment, decimal Deposit)> BookAsync(int day, int hour)
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var made = await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(day), hour, duration = 1, payAtVenue = false, acceptWaiver = true });
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var b = made.Body.Data();
        b.GetProperty("status").GetString().ShouldBe("AwaitingPayment");
        var url = b.GetProperty("paymentUrl").GetString()!;
        url.ShouldContain("/pay/PY-");
        var payment = url.Split("/pay/")[1].Split('?')[0];
        return (player, b.GetProperty("code").GetString()!, payment, b.GetProperty("deposit").GetDecimal());
    }

    [Fact]
    public async Task A_booking_paid_by_instapay_is_confirmed_only_by_the_team()
    {
        var (player, booking, payment, deposit) = await BookAsync(3, 20);
        var status = (await player.GetAsync($"/api/v1/payments/{payment}")).Body.Data();
        status.GetProperty("provider").GetString().ShouldBe("instapay");
        status.GetProperty("instaPay").GetProperty("address").GetString().ShouldBe("owner@instapay");
        status.GetProperty("instaPay").GetProperty("link").GetString()!.ShouldStartWith("https://ipn.eg/");

        (await player.PostAsync($"/api/v1/payments/{payment}/instapay", new { reference = "<script>", sender = "Me" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var (stranger, _) = await kit.PersonAsync(partner: false);
        (await stranger.PostAsync($"/api/v1/payments/{payment}/instapay", new { reference = "123456", sender = "Me" })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await player.GetAsync("/api/v1/admin/instapay")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await player.PostAsync($"/api/v1/admin/instapay/{payment}/confirm")).Status.ShouldBe(HttpStatusCode.Forbidden);

        var sent = await player.PostAsync($"/api/v1/payments/{payment}/instapay", new { reference = "123456789012", sender = "Ahmed 010" });
        sent.Status.ShouldBe(HttpStatusCode.OK, sent.Body.ToString());
        (await player.GetAsync($"/api/v1/bookings/{booking}")).Body.Data().GetProperty("status").GetString().ShouldBe("AwaitingPayment");

        // Sent numbers are never expired by the 30-minute clock: the team decides
        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == booking).ExecuteUpdateAsync(u => u.SetProperty(b => b.CreatedAt, b => b.CreatedAt.AddHours(-3))));
        await api.Services.GetRequiredService<AwaitingPaymentExpiry>().RunOnceAsync(CancellationToken.None);

        var ceo = await api.CeoAsync();
        var queue = (await ceo.GetAsync("/api/v1/admin/instapay")).Body.Data();
        var item = queue.EnumerateArray().Single(i => i.GetProperty("code").GetString() == payment);
        item.GetProperty("reference").GetString().ShouldBe("123456789012");
        item.GetProperty("amount").GetDecimal().ShouldBe(deposit);

        (await ceo.PostAsync($"/api/v1/admin/instapay/{payment}/confirm")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/instapay/{payment}/confirm")).Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await player.GetAsync($"/api/v1/bookings/{booking}")).Body.Data().GetProperty("status").GetString().ShouldBe("Confirmed");
        (await player.GetAsync($"/api/v1/payments/{payment}")).Body.Data().GetProperty("status").GetString().ShouldBe("Paid");
    }

    [Fact]
    public async Task No_number_in_30_minutes_frees_the_hour_and_a_refused_number_fails_the_payment()
    {
        var (_, quiet, _, _) = await BookAsync(4, 19);
        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == quiet).ExecuteUpdateAsync(u => u.SetProperty(b => b.CreatedAt, b => b.CreatedAt.AddMinutes(-20))));
        await api.Services.GetRequiredService<AwaitingPaymentExpiry>().RunOnceAsync(CancellationToken.None);
        (await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == quiet).Select(b => b.Status).SingleAsync())).ShouldBe(SuperApp.Domain.Enums.BookingStatus.AwaitingPayment);
        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == quiet).ExecuteUpdateAsync(u => u.SetProperty(b => b.CreatedAt, b => b.CreatedAt.AddMinutes(-11))));
        await api.Services.GetRequiredService<AwaitingPaymentExpiry>().RunOnceAsync(CancellationToken.None);
        (await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == quiet).Select(b => b.Status).SingleAsync())).ShouldBe(SuperApp.Domain.Enums.BookingStatus.CancelledByCustomer);

        var (player, _, payment, _) = await BookAsync(5, 18);
        (await player.PostAsync($"/api/v1/payments/{payment}/instapay", new { reference = "999", sender = "Me" })).Status.ShouldBe(HttpStatusCode.OK);
        var ceo = await api.CeoAsync();
        (await ceo.PostAsync($"/api/v1/admin/instapay/{payment}/reject", new { reason = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync($"/api/v1/admin/instapay/{payment}/reject", new { reason = "No transfer with this number" })).Status.ShouldBe(HttpStatusCode.OK);
        (await player.GetAsync($"/api/v1/payments/{payment}")).Body.Data().GetProperty("status").GetString().ShouldBe("Failed");
        (await player.PostAsync($"/api/v1/payments/{payment}/instapay", new { reference = "1000", sender = "Me" })).Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }
}
