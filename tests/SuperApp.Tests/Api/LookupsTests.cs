using System.Net;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>Public lookups (API §2.4): open to guests, and internal settings never leak.</summary>
[Trait("Category", "Database")]
public sealed class LookupsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    [Fact]
    public async Task Guests_read_places_and_pitch_options()
    {
        var guest = await api.ClientAsync();
        var governorates = (await guest.GetAsync("/api/v1/lookups/governorates")).Body.Data();
        governorates.GetArrayLength().ShouldBe(27);
        var cairo = governorates.EnumerateArray().Single(g => g.GetProperty("code").GetString() == "C").GetProperty("id").GetInt32();

        var areas = (await guest.GetAsync($"/api/v1/lookups/governorates/{cairo}/areas")).Body.Data();
        areas.EnumerateArray().Select(a => a.GetProperty("nameEn").GetString()).ShouldContain("Zamalek");
        (await guest.GetAsync("/api/v1/lookups/facilities")).Body.Data().GetArrayLength().ShouldBe(14);
        (await guest.GetAsync("/api/v1/lookups/cancellation-policies")).Body.Data().GetArrayLength().ShouldBe(4);
        (await guest.GetAsync("/api/v1/lookups/seller-levels")).Body.Data()[0].GetProperty("key").GetString().ShouldBe("new");
    }

    [Fact]
    public async Task Public_settings_show_plain_values_and_hide_internal_ones()
    {
        var settings = (await (await api.ClientAsync()).GetAsync("/api/v1/lookups/settings/public")).Body.Data();
        settings.GetProperty("vat.rate").GetDecimal().ShouldBe(0.14m);
        settings.GetProperty("popups.timing").GetProperty("interval").GetInt32().ShouldBe(10);
        settings.TryGetProperty("commission.bookings", out _).ShouldBeFalse();
        settings.TryGetProperty("limits.payoutCeo", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Holidays_check_the_year()
    {
        var guest = await api.ClientAsync();
        (await guest.GetAsync("/api/v1/lookups/holidays?year=2026")).Status.ShouldBe(HttpStatusCode.OK);
        var bad = await guest.GetAsync("/api/v1/lookups/holidays?year=1990");
        bad.Status.ShouldBe(HttpStatusCode.BadRequest);
        bad.Body.Code().ShouldBe("bad_year");
    }
}
