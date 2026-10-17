using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>The partner dashboard's venues, pitches and blocked times (API §3.2), with who may do what.</summary>
[Trait("Category", "Database")]
public sealed class PartnerPitchesTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task A_new_pitch_waits_for_review_and_the_reviewers_are_told()
    {
        var (client, _, _, pitch) = await kit.PartnerWithPitchAsync();

        pitch.GetProperty("status").GetString().ShouldBe("WaitingForReview");
        pitch.GetProperty("openingHours").GetArrayLength().ShouldBe(7);
        pitch.GetProperty("facilities").EnumerateArray().Select(f => f.GetString()).ShouldBe(["lights", "parking"], ignoreOrder: true);
        var mine = (await client.GetAsync("/api/v1/partner/pitches")).Body.Data();
        mine.GetArrayLength().ShouldBe(1);
        (await (await api.ClientAsync()).GetAsync($"/api/v1/pitches/{pitch.GetProperty("slug").GetString()}")).Status.ShouldBe(HttpStatusCode.NotFound);
        var ceoTold = await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Notifications.Notification>().AnyAsync(n => n.Title == "New pitch to review: Test Five"));
        ceoTold.ShouldBeTrue();
    }

    [Fact]
    public async Task An_edit_needs_the_latest_version_and_a_rejected_pitch_goes_back_to_review()
    {
        var (client, _, venueId, pitch) = await kit.PartnerWithPitchAsync();
        var id = pitch.GetProperty("id").GetInt64();
        var version = pitch.GetProperty("rowVersion").GetString();

        var saved = await client.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}", PartnerKit.Pitch(venueId, "Test Five Renamed", 650, version));
        saved.Status.ShouldBe(HttpStatusCode.OK, saved.Body.ToString());
        saved.Body.Data().GetProperty("pricePerHour").GetDecimal().ShouldBe(650);
        var stale = await client.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}", PartnerKit.Pitch(venueId, "Old tab", 700, version));
        stale.Status.ShouldBe(HttpStatusCode.Conflict);
        stale.Body.Code().ShouldBe("stale");

        await api.DbAsync(db => db.Set<Pitch>().Where(p => p.Id == id).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PitchStatus.Rejected).SetProperty(p => p.RejectReason, "Photos")));
        var latest = (await client.GetAsync($"/api/v1/partner/pitches/{id}")).Body.Data().GetProperty("rowVersion").GetString();
        var again = await client.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}", PartnerKit.Pitch(venueId, "Test Five", 650, latest));
        again.Body.Data().GetProperty("status").GetString().ShouldBe("WaitingForReview");
        again.Body.Data().GetProperty("rejectReason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Pitch_rules_are_checked()
    {
        var (client, _, venueId, _) = await kit.PartnerWithPitchAsync();
        var badHours = new
        {
            venueId, name = "Bad", format = 6, surface = "Sand", isIndoor = false, pricePerHour = 50m, peakPricePerHour = (decimal?)null, peakFromHour = (int?)null,
            policyKey = "standard", allowCash = true, facilities = Array.Empty<string>(), openingHours = PartnerKit.Week(20, 19),
        };
        var bad = await client.PostAsync("/api/v1/partner/pitches", badHours);
        bad.Status.ShouldBe(HttpStatusCode.BadRequest);
        foreach (var field in new[] { "format", "surface", "pricePerHour", "openingHours[0].closeHour" })
        {
            bad.Body.HasFieldError(field).ShouldBeTrue(field);
        }
        var unknown = await client.PostAsync("/api/v1/partner/pitches", new
        {
            venueId, name = "Good name", format = 7, surface = "NaturalGrass", isIndoor = false, pricePerHour = 900m, peakPricePerHour = (decimal?)null, peakFromHour = (int?)null,
            policyKey = "standard", allowCash = true, facilities = new[] { "jacuzzi" }, openingHours = PartnerKit.Week(),
        });
        unknown.Body.Code().ShouldBe("unknown_facility");
    }

    [Fact]
    public async Task Close_and_reopen_only_from_the_right_status()
    {
        var (client, _, _, pitch) = await kit.PartnerWithPitchAsync();
        var id = pitch.GetProperty("id").GetInt64();

        (await client.PostAsync($"/api/v1/partner/pitches/{id}/close")).Body.Code().ShouldBe("wrong_status");
        await api.DbAsync(db => db.Set<Pitch>().Where(p => p.Id == id).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PitchStatus.Live)));
        (await client.PostAsync($"/api/v1/partner/pitches/{id}/close")).Body.Data().GetProperty("status").GetString().ShouldBe("ClosedByOwner");
        (await client.PostAsync($"/api/v1/partner/pitches/{id}/reopen")).Body.Data().GetProperty("status").GetString().ShouldBe("Live");
    }

    [Fact]
    public async Task Booked_hours_cannot_be_blocked_until_the_booking_is_cancelled()
    {
        var (client, _, _, pitch) = await kit.PartnerWithPitchAsync();
        var id = pitch.GetProperty("id").GetInt64();
        await api.DbAsync(db => db.Set<Pitch>().Where(p => p.Id == id).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PitchStatus.Live)));
        var (player, _) = await kit.PersonAsync(partner: false);
        var booked = await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(5), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true });
        booked.Status.ShouldBe(HttpStatusCode.Created, booked.Body.ToString());

        var blocked = await client.PostAsync($"/api/v1/partner/pitches/{id}/closures", new { fromDate = PartnerKit.Day(5), toDate = PartnerKit.Day(5), fromHour = 18, toHour = 22, reason = "Maintenance" });
        blocked.Status.ShouldBe(HttpStatusCode.Conflict);
        blocked.Body.Code().ShouldBe("bookings_in_range");
        var morning = await client.PostAsync($"/api/v1/partner/pitches/{id}/closures", new { fromDate = PartnerKit.Day(5), toDate = PartnerKit.Day(6), fromHour = 9, toHour = 12, reason = "Maintenance" });
        morning.Status.ShouldBe(HttpStatusCode.Created, morning.Body.ToString());

        var grid = (await player.GetAsync($"/api/v1/pitches/{pitch.GetProperty("slug").GetString()}/availability?from={PartnerKit.Day(5)}&days=1")).Body.Data().GetProperty("days")[0].GetProperty("hours");
        grid.EnumerateArray().Single(h => h.GetProperty("hour").GetInt32() == 10).GetProperty("status").GetString().ShouldBe("closed");
        var closureId = morning.Body.Data().GetProperty("id").GetInt64();
        (await client.SendAsync(HttpMethod.Delete, $"/api/v1/partner/pitches/{id}/closures/{closureId}")).Status.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync($"/api/v1/partner/pitches/{id}/closures")).Body.Data().GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Staff_can_read_but_only_managers_change_and_strangers_see_nothing()
    {
        var (_, _, venueId, pitch) = await kit.PartnerWithPitchAsync();
        var id = pitch.GetProperty("id").GetInt64();
        var (staff, staffId) = await kit.PersonAsync(partner: false);
        await kit.AddStaffAsync(venueId, staffId, PartnerStaffRole.CheckIn);

        (await staff.GetAsync($"/api/v1/partner/pitches/{id}")).Status.ShouldBe(HttpStatusCode.OK);
        var change = await staff.PostAsync($"/api/v1/partner/pitches/{id}/close");
        change.Status.ShouldBe(HttpStatusCode.Forbidden);
        change.Body.Code().ShouldBe("manager_only");

        var (stranger, _) = await kit.PersonAsync(partner: false);
        (await stranger.GetAsync("/api/v1/partner/pitches")).Body.Code().ShouldBe("not_partner");
        var (otherPartner, _) = await kit.PersonAsync(partner: true);
        (await otherPartner.GetAsync($"/api/v1/partner/pitches/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await (await api.ClientAsync()).GetAsync("/api/v1/partner/pitches")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
