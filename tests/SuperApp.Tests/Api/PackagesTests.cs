using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Package sizes and their approval, quote, buy, my packages and the one QR (API §3.5, §3.6).</summary>
[Trait("Category", "Database")]
public sealed class PackagesTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
    private readonly PartnerKit kit = new(api);

    /// <summary>A live pitch whose 4-match package (10% off) the CEO approved.</summary>
    private async Task<(ApiClient Partner, long PitchId, string Slug, long OptionId)> PitchWithPackageAsync()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = pitch.GetProperty("id").GetInt64();
        var saved = await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}/package-options", new { options = new[] { new { matchCount = 4, discountPercent = 10m } } });
        saved.Status.ShouldBe(HttpStatusCode.OK, saved.Body.ToString());
        var optionId = saved.Body.Data()[0].GetProperty("id").GetInt64();
        (await (await api.CeoAsync()).PostAsync($"/api/v1/admin/package-options/{optionId}/approve", new { approvedDiscount = (decimal?)null })).Status.ShouldBe(HttpStatusCode.OK);
        return (partner, id, pitch.GetProperty("slug").GetString()!, optionId);
    }

    [Fact]
    public async Task Package_sizes_wait_for_approval_and_respect_the_ceo_maximum()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = pitch.GetProperty("id").GetInt64();
        var url = $"/api/v1/partner/pitches/{id}/package-options";

        (await partner.SendAsync(HttpMethod.Put, url, new { options = new[] { new { matchCount = 8, discountPercent = 30m } } })).Body.Code().ShouldBe("discount_over_max");
        var saved = (await partner.SendAsync(HttpMethod.Put, url, new { options = new[] { new { matchCount = 4, discountPercent = 10m }, new { matchCount = 8, discountPercent = 20m } } })).Body.Data();
        saved.EnumerateArray().ShouldAllBe(o => o.GetProperty("status").GetString() == "WaitingForApproval");
        var slug = pitch.GetProperty("slug").GetString();
        (await partner.GetAsync($"/api/v1/pitches/{slug}")).Body.Data().GetProperty("packageOptions").GetArrayLength().ShouldBe(0);

        var ceo = await api.CeoAsync();
        var waiting = (await ceo.GetAsync("/api/v1/admin/package-options?pageSize=100")).Body.Data();
        var eight = waiting.EnumerateArray().Single(o => o.GetProperty("pitchId").GetInt64() == id && o.GetProperty("matchCount").GetInt32() == 8).GetProperty("id").GetInt64();
        (await ceo.PostAsync($"/api/v1/admin/package-options/{eight}/approve", new { approvedDiscount = 25m })).Body.Code().ShouldBe("approved_discount");
        (await ceo.PostAsync($"/api/v1/admin/package-options/{eight}/approve", new { approvedDiscount = 15m })).Status.ShouldBe(HttpStatusCode.OK);
        var offered = (await partner.GetAsync($"/api/v1/pitches/{slug}")).Body.Data().GetProperty("packageOptions");
        offered.GetArrayLength().ShouldBe(1);
        offered[0].GetProperty("discountPercent").GetDecimal().ShouldBe(15);

        (await partner.SendAsync(HttpMethod.Put, url, new { options = new[] { new { matchCount = 4, discountPercent = 10m } } })).Body.Data().GetArrayLength().ShouldBe(1);
        (await partner.GetAsync($"/api/v1/pitches/{slug}")).Body.Data().GetProperty("packageOptions").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task A_package_skips_taken_weeks_books_every_match_and_is_bought_once()
    {
        var (_, _, slug, optionId) = await PitchWithPackageAsync();
        var start = PartnerKit.Day(7);
        var (other, _) = await kit.PersonAsync(partner: false);
        (await other.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(14), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true })).Status.ShouldBe(HttpStatusCode.Created);

        var quote = (await other.GetAsync($"/api/v1/packages/quote?pitchSlug={slug}&optionId={optionId}&startsOn={start}&hour=20")).Body.Data();
        quote.GetProperty("weeks").GetArrayLength().ShouldBe(5);
        quote.GetProperty("skipped").GetInt32().ShouldBe(1);
        quote.GetProperty("listPrice").GetDecimal().ShouldBe(3000);     // 4 peak hours at 750
        quote.GetProperty("total").GetDecimal().ShouldBe(4 * (675 + 20));

        var (player, _) = await kit.PersonAsync(partner: false);
        var buy = new { pitchSlug = slug, optionId, startsOn = start, hour = 20, duration = 1, acceptWaiver = true };
        var bought = await player.PostAsync("/api/v1/packages", buy);
        bought.Status.ShouldBe(HttpStatusCode.Created, bought.Body.ToString());
        var package = bought.Body.Data();
        package.GetProperty("code").GetString()!.ShouldStartWith("PK-");
        package.GetProperty("matches").GetArrayLength().ShouldBe(4);
        package.GetProperty("qrToken").GetString().ShouldNotBeNullOrEmpty();
        (await player.PostAsync("/api/v1/packages", buy)).Body.Data().GetProperty("code").GetString().ShouldBe(package.GetProperty("code").GetString());
        (await player.GetAsync("/api/v1/packages/mine")).Body.Data().GetArrayLength().ShouldBe(1);
        var match = package.GetProperty("matches")[0].GetProperty("code").GetString();
        (await player.GetAsync($"/api/v1/bookings/{match}")).Body.Data().GetProperty("packageCode").GetString().ShouldBe(package.GetProperty("code").GetString());

        (await other.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = start, hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true })).Body.Code().ShouldBe("slot_taken");
        (await other.GetAsync($"/api/v1/packages/{package.GetProperty("code").GetString()}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task One_qr_checks_in_todays_match_and_counts_it()
    {
        var (partner, _, slug, optionId) = await PitchWithPackageAsync();
        var (player, _) = await kit.PersonAsync(partner: false);
        var package = (await player.PostAsync("/api/v1/packages", new { pitchSlug = slug, optionId, startsOn = PartnerKit.Day(7), hour = 20, duration = 1, acceptWaiver = true })).Body.Data();
        var code = package.GetProperty("code").GetString()!;
        var token = package.GetProperty("qrToken").GetString()!;

        (await ScanAsync(partner, token)).GetProperty("reason").GetString().ShouldBe("wrong_day");
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Cairo);
        var first = package.GetProperty("matches")[0].GetProperty("code").GetString();
        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == first)
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.Date, DateOnly.FromDateTime(now)).SetProperty(b => b.StartHour, (byte)now.Hour)));

        var accepted = await ScanAsync(partner, token);
        accepted.GetProperty("result").GetString().ShouldBe("accepted");
        accepted.GetProperty("match").GetString().ShouldBe("1 of 4");
        (await ScanAsync(partner, token)).GetProperty("reason").GetString().ShouldBe("already_used");
        (await player.GetAsync($"/api/v1/packages/{code}")).Body.Data().GetProperty("usedCount").GetInt32().ShouldBe(1);

        await api.DbAsync(db => db.Set<Package>().Where(p => p.Code == code).ExecuteUpdateAsync(u => u.SetProperty(p => p.UsedCount, (byte)4).SetProperty(p => p.Status, PackageStatus.Finished)));
        (await ScanAsync(partner, token)).GetProperty("reason").GetString().ShouldBe("package_finished");
    }

    private static async Task<JsonElement> ScanAsync(ApiClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/check-in/scan") { Content = JsonContent.Create(new { token }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var response = await client.Http.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Data();
    }
}
