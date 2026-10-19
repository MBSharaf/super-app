using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Tests.Api;

/// <summary>Free slots and alerts (API §3.7).</summary>
[Trait("Category", "Database")]
public sealed class FreeSlotsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task A_posted_free_hour_is_discounted_listed_and_followers_are_told()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = pitch.GetProperty("id").GetInt64();
        var slug = pitch.GetProperty("slug").GetString()!;
        var area = await api.DbAsync(db => db.Set<Area>().OrderBy(a => a.Id).Select(a => a.Id).FirstAsync());
        var (fan, fanId) = await kit.PersonAsync(partner: false);
        var (areaFan, areaFanId) = await kit.PersonAsync(partner: false);
        var (late, lateId) = await kit.PersonAsync(partner: false);
        (await fan.PostAsync("/api/v1/alerts", new { pitchSlug = slug })).Status.ShouldBe(HttpStatusCode.Created);
        (await areaFan.PostAsync("/api/v1/alerts", new { areaId = area, fromHour = 18, toHour = 23 })).Status.ShouldBe(HttpStatusCode.Created);
        (await late.PostAsync("/api/v1/alerts", new { areaId = area, fromHour = 6, toHour = 10 })).Status.ShouldBe(HttpStatusCode.Created);
        (await fan.PostAsync("/api/v1/alerts", new { })).Status.ShouldBe(HttpStatusCode.BadRequest);

        var day = PartnerKit.Day(2);
        var posted = await partner.PostAsync("/api/v1/partner/free-slots", new { pitchId = id, date = day, hour = 20, discountPercent = 30m });
        posted.Status.ShouldBe(HttpStatusCode.Created, posted.Body.ToString());
        posted.Body.Data().GetProperty("price").GetDecimal().ShouldBe(525);   // peak 750 − 30%
        (await partner.PostAsync("/api/v1/partner/free-slots", new { pitchId = id, date = day, hour = 20, discountPercent = 20m })).Body.Code().ShouldBe("already_posted");

        var hour = (await fan.GetAsync($"/api/v1/pitches/{slug}/availability?from={day}&days=1")).Body.Data().GetProperty("days")[0].GetProperty("hours")
            .EnumerateArray().Single(h => h.GetProperty("hour").GetInt32() == 20);
        hour.GetProperty("price").GetDecimal().ShouldBe(525);
        (await fan.GetAsync($"/api/v1/free-slots?areaId={area}")).Body.Data().EnumerateArray().ShouldContain(s => s.GetProperty("pitchSlug").GetString() == slug);

        bool Told(Guid u) => api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.UserId == u && n.Title.StartsWith("Free slot at"))).Result;
        Told(fanId).ShouldBeTrue();
        Told(areaFanId).ShouldBeTrue();
        Told(lateId).ShouldBeFalse();

        await fan.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = day, hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true });
        (await fan.GetAsync($"/api/v1/free-slots?areaId={area}")).Body.Data().EnumerateArray().ShouldNotContain(s => s.GetProperty("pitchSlug").GetString() == slug);
    }

    [Fact]
    public async Task Posting_has_a_daily_limit_and_can_be_blocked()
    {
        var (partner, _, venueId, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = pitch.GetProperty("id").GetInt64();
        for (var h = 10; h < 15; h++)
        {
            (await partner.PostAsync("/api/v1/partner/free-slots", new { pitchId = id, date = PartnerKit.Day(3), hour = h, discountPercent = 10m })).Status.ShouldBe(HttpStatusCode.Created);
        }
        (await partner.PostAsync("/api/v1/partner/free-slots", new { pitchId = id, date = PartnerKit.Day(3), hour = 15, discountPercent = 10m })).Body.Code().ShouldBe("daily_limit");
        var mine = (await partner.GetAsync("/api/v1/partner/free-slots")).Body.Data();
        mine.GetArrayLength().ShouldBe(5);
        (await partner.SendAsync(HttpMethod.Delete, $"/api/v1/partner/free-slots/{mine[0].GetProperty("id").GetInt64()}")).Status.ShouldBe(HttpStatusCode.OK);

        var partnerId = await api.DbAsync(db => db.Set<Venue>().Where(v => v.Id == venueId).Select(v => v.PartnerId).SingleAsync());
        await api.DbAsync(db => db.Set<Partner>().Where(p => p.Id == partnerId).ExecuteUpdateAsync(u => u.SetProperty(p => p.SlotPostingBlocked, true)));
        (await partner.PostAsync("/api/v1/partner/free-slots", new { pitchId = id, date = PartnerKit.Day(4), hour = 10, discountPercent = 10m })).Body.Code().ShouldBe("posting_blocked");
    }
}
