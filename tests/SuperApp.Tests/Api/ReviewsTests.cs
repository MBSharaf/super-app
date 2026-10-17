using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Reviews (API §3.10): after playing only, once per booking, owner replies and reports, moderators hide or keep.</summary>
[Trait("Category", "Database")]
public sealed class ReviewsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    /// <summary>A live pitch and a player with a booking there (moved to yesterday when <paramref name="played"/>).</summary>
    private async Task<(ApiClient Partner, long VenueId, string Slug, ApiClient Player, string Code)> GameAsync(bool played)
    {
        var (partner, _, venueId, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString()!;
        var (player, _) = await kit.PersonAsync(partner: false);
        var code = (await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(3), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true }))
            .Body.Data().GetProperty("code").GetString()!;
        if (played)
        {
            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == code).ExecuteUpdateAsync(u => u.SetProperty(b => b.Date, yesterday)));
        }
        return (partner, venueId, slug, player, code);
    }

    private static object Review(int stars = 4, string text = "Great turf and bright lights, will come back.") => new { stars, text };

    [Fact]
    public async Task A_played_game_is_reviewed_once_and_the_rating_follows()
    {
        var (_, _, slug, player, code) = await GameAsync(played: false);
        (await player.PostAsync($"/api/v1/bookings/{code}/review", Review())).Body.Code().ShouldBe("not_played");

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == code).ExecuteUpdateAsync(u => u.SetProperty(b => b.Date, yesterday)));
        (await player.PostAsync($"/api/v1/bookings/{code}/review", Review(text: "short"))).Status.ShouldBe(HttpStatusCode.BadRequest);
        var created = await player.PostAsync($"/api/v1/bookings/{code}/review", Review());
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        created.Body.Data().GetProperty("customerName").GetString().ShouldBe("Test");
        (await player.PostAsync($"/api/v1/bookings/{code}/review", Review(5))).Body.Code().ShouldBe("already_reviewed");

        var page = (await player.GetAsync($"/api/v1/pitches/{slug}")).Body.Data().GetProperty("card");
        page.GetProperty("rating").GetDecimal().ShouldBe(4);
        page.GetProperty("reviewsCount").GetInt32().ShouldBe(1);
        (await player.GetAsync($"/api/v1/pitches/{slug}/reviews")).Body.Data().GetArrayLength().ShouldBe(1);
        (await player.GetAsync($"/api/v1/bookings/{code}/review")).Body.Data().GetProperty("stars").GetInt32().ShouldBe(4);
        var (stranger, _) = await kit.PersonAsync(partner: false);
        (await stranger.PostAsync($"/api/v1/bookings/{code}/review", Review())).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_owner_replies_and_reports_and_a_hidden_review_leaves_the_rating()
    {
        var (partner, venueId, slug, player, code) = await GameAsync(played: true);
        await player.PostAsync($"/api/v1/bookings/{code}/review", Review(1, "Terrible, the lights were off the whole game."));
        var id = (await partner.GetAsync("/api/v1/partner/reviews")).Body.Data()[0].GetProperty("id").GetInt64();

        (await partner.PostAsync($"/api/v1/partner/reviews/{id}/reply", new { text = "Sorry — the lights were fixed the next day." })).Status.ShouldBe(HttpStatusCode.OK);
        (await player.GetAsync($"/api/v1/pitches/{slug}/reviews")).Body.Data()[0].GetProperty("reply").GetString()!.ShouldStartWith("Sorry");
        (await partner.PostAsync($"/api/v1/partner/reviews/{id}/report", new { reason = "This player never came to the game." })).Status.ShouldBe(HttpStatusCode.OK);
        (await partner.PostAsync($"/api/v1/partner/reviews/{id}/report", new { reason = "Again please." })).Body.Code().ShouldBe("already_reported");

        var (desk, deskId) = await kit.PersonAsync(partner: false);
        await kit.AddStaffAsync(venueId, deskId, PartnerStaffRole.CheckIn);
        (await desk.PostAsync($"/api/v1/partner/reviews/{id}/reply", new { text = "Hi" })).Body.Code().ShouldBe("manager_only");
        var (other, _, _, _) = await kit.PartnerWithPitchAsync();
        (await other.PostAsync($"/api/v1/partner/reviews/{id}/reply", new { text = "Not mine" })).Status.ShouldBe(HttpStatusCode.NotFound);

        var ceo = await api.CeoAsync();
        var report = (await ceo.GetAsync("/api/v1/admin/review-reports?pageSize=100")).Body.Data().EnumerateArray().Single(r => r.GetProperty("reviewId").GetInt64() == id);
        (await ceo.PostAsync($"/api/v1/admin/review-reports/{report.GetProperty("id").GetInt64()}/hide")).Status.ShouldBe(HttpStatusCode.OK);
        (await player.GetAsync($"/api/v1/pitches/{slug}/reviews")).Body.Data().GetArrayLength().ShouldBe(0);
        (await player.GetAsync($"/api/v1/pitches/{slug}")).Body.Data().GetProperty("card").GetProperty("reviewsCount").GetInt32().ShouldBe(0);
        (await ceo.PostAsync($"/api/v1/admin/review-reports/{report.GetProperty("id").GetInt64()}/keep")).Body.Code().ShouldBe("wrong_status");
        (await partner.GetAsync("/api/v1/admin/review-reports")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
