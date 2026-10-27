using System.Net;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>GET /admin/revenue: what the platform earned from a paid booking, by Cairo day, and who may see it.</summary>
[Trait("Category", "Database")]
public sealed class RevenueTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task A_paid_booking_shows_as_commission_fee_and_gmv_today()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var ceo = await api.CeoAsync();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"))).ToString("yyyy-MM-dd");
        var before = (await ceo.GetAsync($"/api/v1/admin/revenue?from={today}&to={today}")).Body.Data().GetProperty("daily")[0];

        var booking = (await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(5), hour = 21, duration = 1, payAtVenue = false, acceptWaiver = true })).Body.Data();
        var revenue = await ceo.GetAsync($"/api/v1/admin/revenue?from={today}&to={today}");
        revenue.Status.ShouldBe(HttpStatusCode.OK, revenue.Body.ToString());
        var day = revenue.Body.Data().GetProperty("daily")[0];
        (day.GetProperty("fees").GetDecimal() - before.GetProperty("fees").GetDecimal()).ShouldBe(booking.GetProperty("serviceFee").GetDecimal());
        (day.GetProperty("gmv").GetDecimal() - before.GetProperty("gmv").GetDecimal()).ShouldBe(booking.GetProperty("total").GetDecimal());
        (day.GetProperty("bookings").GetDecimal() - before.GetProperty("bookings").GetDecimal()).ShouldBeGreaterThan(0);
        revenue.Body.Data().GetProperty("owners").GetArrayLength().ShouldBeGreaterThan(0);

        (await ceo.GetAsync("/api/v1/admin/revenue")).Body.Data().GetProperty("daily").GetArrayLength().ShouldBe(30);
        (await ceo.GetAsync($"/api/v1/admin/revenue?from={today}&to=2020-01-01")).Body.Code().ShouldBe("range");
        (await player.GetAsync("/api/v1/admin/revenue")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
