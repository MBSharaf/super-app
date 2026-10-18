using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>The waitlist (API §3.7): join a booked hour, be told when it frees up, and close the entry by booking it.</summary>
[Trait("Category", "Database")]
public sealed class WaitlistTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private static object Book(string slug, string date, int hour) => new { pitchSlug = slug, date, hour, duration = 1, payAtVenue = false, acceptWaiver = true };

    private Task<bool> ToldAsync(Guid userId) =>
        api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.UserId == userId && n.Title.StartsWith("An hour you wanted is free")));

    [Fact]
    public async Task A_freed_hour_tells_everyone_waiting_and_booking_it_closes_the_entry()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString()!;
        var day = PartnerKit.Day(4);
        var (first, _) = await kit.PersonAsync(partner: false);
        var (second, secondId) = await kit.PersonAsync(partner: false);

        (await second.PostAsync("/api/v1/waitlist", new { pitchSlug = slug, date = day, hour = 21 })).Body.Code().ShouldBe("available");
        var code = (await first.PostAsync("/api/v1/bookings", Book(slug, day, 21))).Body.Data().GetProperty("code").GetString();
        var joined = await second.PostAsync("/api/v1/waitlist", new { pitchSlug = slug, date = day, hour = 21 });
        joined.Status.ShouldBe(HttpStatusCode.Created, joined.Body.ToString());
        joined.Body.Data().GetProperty("waiting").GetInt32().ShouldBe(1);
        (await second.PostAsync("/api/v1/waitlist", new { pitchSlug = slug, date = day, hour = 21 })).Body.Code().ShouldBe("already_waiting");

        (await first.PostAsync($"/api/v1/bookings/{code}/cancel")).Status.ShouldBe(HttpStatusCode.OK);
        (await ToldAsync(secondId)).ShouldBeTrue();
        (await second.GetAsync("/api/v1/waitlist/mine")).Body.Data()[0].GetProperty("status").GetString().ShouldBe("FreeNow");

        (await second.PostAsync("/api/v1/bookings", Book(slug, day, 21))).Status.ShouldBe(HttpStatusCode.Created);
        (await second.GetAsync("/api/v1/waitlist/mine")).Body.Data().GetArrayLength().ShouldBe(0);
        (await api.DbAsync(db => db.Set<WaitlistEntry>().Where(w => w.UserId == secondId).Select(w => w.Status).SingleAsync())).ShouldBe(WaitlistStatus.Booked);
    }

    [Fact]
    public async Task A_venue_cancellation_also_tells_the_waitlist_and_players_can_leave_it()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString()!;
        var day = PartnerKit.Day(5);
        var (booker, _) = await kit.PersonAsync(partner: false);
        var (waiting, waitingId) = await kit.PersonAsync(partner: false);
        var (leaver, leaverId) = await kit.PersonAsync(partner: false);
        var code = (await booker.PostAsync("/api/v1/bookings", Book(slug, day, 19))).Body.Data().GetProperty("code").GetString();
        await waiting.PostAsync("/api/v1/waitlist", new { pitchSlug = slug, date = day, hour = 19 });
        var leave = (await leaver.PostAsync("/api/v1/waitlist", new { pitchSlug = slug, date = day, hour = 19 })).Body.Data().GetProperty("id").GetInt64();

        (await waiting.SendAsync(HttpMethod.Delete, $"/api/v1/waitlist/{leave}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await leaver.SendAsync(HttpMethod.Delete, $"/api/v1/waitlist/{leave}")).Status.ShouldBe(HttpStatusCode.OK);
        (await partner.PostAsync($"/api/v1/partner/bookings/{code}/cancel", new { reason = "Pitch maintenance" })).Status.ShouldBe(HttpStatusCode.OK);

        (await ToldAsync(waitingId)).ShouldBeTrue();
        (await ToldAsync(leaverId)).ShouldBeFalse();
    }
}
