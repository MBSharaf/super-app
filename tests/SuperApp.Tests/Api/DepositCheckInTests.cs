using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>
/// Owner's rules (2026-11-01) end to end: the app takes only the deposit, the venue scans the QR and collects the rest,
/// a late player sees the minutes left, and every EGP paid earns 1 Super Point (taken back on a refund).
/// </summary>
[Trait("Category", "Database")]
public sealed class DepositCheckInTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
    private readonly PartnerKit kit = new(api);

    private static object Booking(string slug, string date, int hour) => new { pitchSlug = slug, date, hour, duration = 1, payAtVenue = false, acceptWaiver = true };

    private Task<int> PointsAsync(Guid userId) => api.DbAsync(db => db.Set<PointEntry>().Where(p => p.UserId == userId).SumAsync(p => p.Points));

    private static async Task<JsonElement> ScanAsync(ApiClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/check-in/scan") { Content = JsonContent.Create(new { token, device = "Front desk" }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await client.Http.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Data();
    }

    [Fact]
    public async Task The_app_takes_the_deposit_and_the_venue_collects_the_rest_at_the_scan()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, playerId) = await kit.PersonAsync(partner: false);
        var made = await player.PostAsync("/api/v1/bookings", Booking(pitch.GetProperty("slug").GetString()!, PartnerKit.Day(5), 20));
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var booked = made.Body.Data();
        var (total, deposit, due) = (booked.GetProperty("total").GetDecimal(), booked.GetProperty("deposit").GetDecimal(), booked.GetProperty("balanceDue").GetDecimal());
        deposit.ShouldBe(Math.Max(Math.Round(total * 0.2m, 0, MidpointRounding.AwayFromZero), Math.Min(100, total)));
        (deposit + due).ShouldBe(total);
        (await PointsAsync(playerId)).ShouldBe((int)deposit);   // 1 point per EGP paid in the app

        // The player turns up 0–59 minutes after kick-off: the hour still ends on time
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Cairo);
        var code = booked.GetProperty("code").GetString();
        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == code)
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.Date, DateOnly.FromDateTime(now)).SetProperty(b => b.StartHour, (byte)now.Hour)));
        var scan = await ScanAsync(partner, booked.GetProperty("qrToken").GetString()!);
        scan.GetProperty("result").GetString().ShouldBe("accepted");
        scan.GetProperty("balanceDue").GetDecimal().ShouldBe(due);
        scan.GetProperty("balancePaidAt").ValueKind.ShouldBe(JsonValueKind.String);
        (scan.GetProperty("lateMinutes").GetInt32() + scan.GetProperty("minutesLeft").GetInt32()).ShouldBeInRange(59, 60);
        scan.GetProperty("endHour").GetInt32().ShouldBe((now.Hour + 1) % 24);
        scan.GetProperty("message").GetString()!.ShouldContain($"Collect {due:0} EGP in cash");

        (await PointsAsync(playerId)).ShouldBe((int)total);   // the rest, paid at the pitch, earns its points too
        var partnerRow = (await partner.GetAsync($"/api/v1/partner/bookings?q={code}")).Body.Data()[0];
        partnerRow.GetProperty("deposit").GetDecimal().ShouldBe(deposit);
        partnerRow.GetProperty("balancePaidAt").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Cash_bookings_are_refused_and_a_cancel_refunds_the_deposit_and_its_points()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString()!;
        var (player, playerId) = await kit.PersonAsync(partner: false);
        var cash = await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(6), hour = 20, duration = 1, payAtVenue = true, acceptWaiver = true });
        cash.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        cash.Body.Code().ShouldBe("deposit_required");

        var booked = (await player.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(6), 20))).Body.Data();
        var deposit = booked.GetProperty("deposit").GetDecimal();
        (await PointsAsync(playerId)).ShouldBe((int)deposit);
        var cancelled = (await player.PostAsync($"/api/v1/bookings/{booked.GetProperty("code").GetString()}/cancel")).Body.Data();
        cancelled.GetProperty("refundAmount").GetDecimal().ShouldBe(deposit);
        (await PointsAsync(playerId)).ShouldBe(0);
        (await api.DbAsync(db => db.Set<PointEntry>().CountAsync(p => p.UserId == playerId && p.Kind == PointKind.Refund))).ShouldBe(1);
    }
}
