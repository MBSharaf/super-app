using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Cancellation requests (owner's rule, 2026-11-07): paid things are cancelled only after the team approves.</summary>
[Trait("Category", "Database")]
public sealed class CancellationsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private async Task<(ApiClient Player, string Booking)> BookedAsync(int day)
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var code = (await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(day), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true }))
            .Body.Data().GetProperty("code").GetString()!;
        return (player, code);
    }

    [Fact]
    public async Task A_paid_booking_needs_a_request_and_a_refusal_keeps_it()
    {
        var (player, booking) = await BookedAsync(4);
        (await player.PostAsync($"/api/v1/bookings/{booking}/cancel")).Body.Code().ShouldBe("request_required");

        var asked = await player.PostAsync("/api/v1/cancellations", new { kind = "booking", reference = booking, reason = "Team can't make it" });
        asked.Status.ShouldBe(HttpStatusCode.Created, asked.Body.ToString());
        var request = asked.Body.Data();
        var code = request.GetProperty("code").GetString()!;
        code.ShouldStartWith("CX-");
        request.GetProperty("status").GetString().ShouldBe("Pending");
        (request.GetProperty("dueAt").GetDateTime() - request.GetProperty("createdAt").GetDateTime()).TotalHours.ShouldBe(2, 0.01);
        (await player.PostAsync("/api/v1/cancellations", new { kind = "booking", reference = booking })).Body.Code().ShouldBe("already_requested");
        (await player.GetAsync("/api/v1/admin/cancellations")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await api.DbAsync(db => db.Set<OutboxEvent>().AnyAsync(e => e.Type == "cancellation.requested" && e.PayloadJson.Contains(code)))).ShouldBeTrue();

        var ceo = await api.CeoAsync();
        (await ceo.GetAsync("/api/v1/admin/cancellations")).Body.Data().EnumerateArray().ShouldContain(r => r.GetProperty("code").GetString() == code);
        (await ceo.PostAsync($"/api/v1/admin/cancellations/{code}/reject", new { note = "no" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var refused = (await ceo.PostAsync($"/api/v1/admin/cancellations/{code}/reject", new { note = "The pitch owner already turned others away." })).Body.Data();
        refused.GetProperty("status").GetString().ShouldBe("Rejected");
        (await ceo.PostAsync($"/api/v1/admin/cancellations/{code}/approve", new { })).Body.Code().ShouldBe("already_answered");

        (await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == booking).Select(b => b.Status).SingleAsync())).ShouldBe(BookingStatus.Confirmed);
        (await player.GetAsync("/api/v1/cancellations/mine")).Body.Data()[0].GetProperty("note").GetString().ShouldBe("The pitch owner already turned others away.");
        (await api.DbAsync(db => db.Set<OutboxEvent>().AnyAsync(e => e.Type == "cancellation.rejected" && e.PayloadJson.Contains(code)))).ShouldBeTrue();
    }

    [Fact]
    public async Task An_approved_request_cancels_and_refunds_only_the_owner_s_booking()
    {
        var (player, booking) = await BookedAsync(5);
        var (stranger, _) = await kit.PersonAsync(partner: false);
        (await stranger.PostAsync("/api/v1/cancellations", new { kind = "booking", reference = booking })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await player.PostAsync("/api/v1/cancellations", new { kind = "pitch", reference = booking })).Status.ShouldBe(HttpStatusCode.BadRequest);

        var approved = await api.CancelThroughRequestAsync(player, "booking", booking);
        approved.GetProperty("status").GetString().ShouldBe("Approved");
        approved.GetProperty("refunded").GetDecimal().ShouldBeGreaterThan(0);
        (await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == booking).Select(b => b.Status).SingleAsync())).ShouldBe(BookingStatus.CancelledByCustomer);
        (await api.DbAsync(db => db.Set<OutboxEvent>().AnyAsync(e => e.Type == "cancellation.approved"))).ShouldBeTrue();
    }
}
