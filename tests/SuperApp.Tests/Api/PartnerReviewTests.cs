using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Pitch photos, deleting a pitch, and the admin's review of new pitches (API §3.2, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class PartnerReviewTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task Photos_are_added_ordered_and_a_live_pitch_keeps_one()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync();
        var id = pitch.GetProperty("id").GetInt64();

        var added = await partner.PostFormAsync($"/api/v1/partner/pitches/{id}/photos", new { }, ("photos", "a.png", Png), ("photos", "b.png", Png));
        added.Status.ShouldBe(HttpStatusCode.Created, added.Body.ToString());
        var ids = added.Body.Data().EnumerateArray().Select(p => p.GetProperty("id").GetInt64()).ToList();
        ids.Count.ShouldBe(2);
        added.Body.Data()[0].GetProperty("url").GetString()!.ShouldStartWith("/api/v1/files/");

        var ordered = await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}/photos/order", new { photoIds = new[] { ids[1], ids[0] } });
        ordered.Body.Data()[0].GetProperty("id").GetInt64().ShouldBe(ids[1]);
        (await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}/photos/order", new { photoIds = new[] { ids[1] } })).Body.Code().ShouldBe("photo_ids");

        (await partner.SendAsync(HttpMethod.Delete, $"/api/v1/partner/pitches/{id}/photos/{ids[0]}")).Body.Data().GetArrayLength().ShouldBe(1);
        await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Pitches.Pitch>().Where(p => p.Id == id).ExecuteUpdateAsync(u => u.SetProperty(p => p.Status, PitchStatus.Live)));
        (await partner.SendAsync(HttpMethod.Delete, $"/api/v1/partner/pitches/{id}/photos/{ids[1]}")).Body.Code().ShouldBe("last_photo");

        var many = Enumerable.Range(0, 10).Select(i => ("photos", $"{i}.png", Png)).ToArray();
        (await partner.PostFormAsync($"/api/v1/partner/pitches/{id}/photos", new { }, many)).Body.Code().ShouldBe("too_many_photos");
    }

    [Fact]
    public async Task Only_the_owner_deletes_a_pitch_and_its_upcoming_games_are_refunded()
    {
        var (owner, _, venueId, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = pitch.GetProperty("id").GetInt64();
        var slug = pitch.GetProperty("slug").GetString();
        var (player, _) = await kit.PersonAsync(partner: false);
        var code = (await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(3), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true }))
            .Body.Data().GetProperty("code").GetString();
        var (manager, managerId) = await kit.PersonAsync(partner: false);
        await kit.AddStaffAsync(venueId, managerId, PartnerStaffRole.Manager);

        (await manager.SendAsync(HttpMethod.Delete, $"/api/v1/partner/pitches/{id}")).Body.Code().ShouldBe("owner_only");
        var deleted = await owner.SendAsync(HttpMethod.Delete, $"/api/v1/partner/pitches/{id}");
        deleted.Status.ShouldBe(HttpStatusCode.Accepted, deleted.Body.ToString());
        deleted.Body.Data().GetProperty("cancelledBookings").GetInt32().ShouldBe(1);

        (await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == code).Select(b => b.Status).SingleAsync())).ShouldBe(BookingStatus.CancelledByVenue);
        (await owner.GetAsync("/api/v1/partner/pitches")).Body.Data().GetArrayLength().ShouldBe(0);
        (await player.GetAsync($"/api/v1/pitches/{slug}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_admin_rejects_with_a_reason_then_approves_and_the_pitch_goes_live()
    {
        var (partner, _, venueId, pitch) = await kit.PartnerWithPitchAsync();
        var id = pitch.GetProperty("id").GetInt64();
        var ceo = await api.CeoAsync();

        var waiting = (await ceo.GetAsync("/api/v1/admin/pitches")).Body.Data();
        waiting.EnumerateArray().ShouldContain(p => p.GetProperty("id").GetInt64() == id && p.GetProperty("businessName").GetString() == "Test Arena Co");
        (await ceo.GetAsync($"/api/v1/admin/pitches/{id}")).Body.Data().GetProperty("pitch").GetProperty("openingHours").GetArrayLength().ShouldBe(7);

        (await ceo.PostAsync($"/api/v1/admin/pitches/{id}/reject", new { reason = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync($"/api/v1/admin/pitches/{id}/reject", new { reason = "Please add real photos of the pitch." })).Status.ShouldBe(HttpStatusCode.OK);
        var mine = (await partner.GetAsync($"/api/v1/partner/pitches/{id}")).Body.Data();
        mine.GetProperty("status").GetString().ShouldBe("Rejected");
        mine.GetProperty("rejectReason").GetString().ShouldBe("Please add real photos of the pitch.");
        (await ceo.PostAsync($"/api/v1/admin/pitches/{id}/approve")).Body.Code().ShouldBe("wrong_status");

        var resent = await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}", PartnerKit.Pitch(venueId, rowVersion: mine.GetProperty("rowVersion").GetString()));
        resent.Body.Data().GetProperty("status").GetString().ShouldBe("WaitingForReview");
        (await ceo.PostAsync($"/api/v1/admin/pitches/{id}/approve")).Body.Data().GetProperty("summary").GetProperty("status").GetString().ShouldBe("Live");
        (await partner.GetAsync($"/api/v1/pitches/{pitch.GetProperty("slug").GetString()}")).Status.ShouldBe(HttpStatusCode.OK);

        (await partner.GetAsync("/api/v1/admin/pitches")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
