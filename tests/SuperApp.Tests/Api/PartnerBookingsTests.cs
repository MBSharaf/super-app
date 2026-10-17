using System.Net;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>The venue's bookings (API §3.4): list, walk-in, no-show, venue cancellation, Excel export, and who may do what.</summary>
[Trait("Category", "Database")]
public sealed class PartnerBookingsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private static object Booking(string slug, string date, int hour) => new { pitchSlug = slug, date, hour, duration = 1, payAtVenue = false, acceptWaiver = true };

    [Fact]
    public async Task A_walk_in_takes_the_hour_without_fee_or_commission()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = pitch.GetProperty("id").GetInt64();
        var slug = pitch.GetProperty("slug").GetString()!;

        var walkIn = await partner.PostAsync("/api/v1/partner/bookings/walk-in", new { pitchId = id, date = PartnerKit.Day(2), hour = 19, duration = 2, name = "Captain Karim", phone = "01012345678" });
        walkIn.Status.ShouldBe(HttpStatusCode.Created, walkIn.Body.ToString());
        var b = walkIn.Body.Data();
        b.GetProperty("source").GetString().ShouldBe("WalkIn");
        b.GetProperty("customerName").GetString().ShouldBe("Captain Karim");
        b.GetProperty("serviceFee").GetDecimal().ShouldBe(0);
        b.GetProperty("commissionRate").GetDecimal().ShouldBe(0);
        b.GetProperty("total").GetDecimal().ShouldBe(1500);   // 2 peak hours at 750

        var (player, _) = await kit.PersonAsync(partner: false);
        (await player.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(2), 20))).Body.Code().ShouldBe("slot_taken");
        (await partner.PostAsync("/api/v1/partner/bookings/walk-in", new { pitchId = id, date = PartnerKit.Day(2), hour = 19, duration = 1, name = "Second" })).Status.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_list_filters_searches_and_stays_private_to_the_partner()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var code = (await player.PostAsync("/api/v1/bookings", Booking(pitch.GetProperty("slug").GetString()!, PartnerKit.Day(3), 20))).Body.Data().GetProperty("code").GetString();

        var upcoming = (await partner.GetAsync("/api/v1/partner/bookings")).Body.Data();
        upcoming.EnumerateArray().Single().GetProperty("code").GetString().ShouldBe(code);
        upcoming[0].GetProperty("customerName").GetString().ShouldBe("Test Person");
        (await partner.GetAsync($"/api/v1/partner/bookings?q={code}")).Body.Data().GetArrayLength().ShouldBe(1);
        (await partner.GetAsync("/api/v1/partner/bookings?q=nobody-like-this")).Body.Data().GetArrayLength().ShouldBe(0);
        (await partner.GetAsync("/api/v1/partner/bookings?status=cancelled")).Body.Data().GetArrayLength().ShouldBe(0);
        (await partner.GetAsync("/api/v1/partner/bookings?status=soon")).Status.ShouldBe(HttpStatusCode.BadRequest);

        var (other, _, _, _) = await kit.PartnerWithPitchAsync();
        (await other.GetAsync("/api/v1/partner/bookings?status=all")).Body.Data().GetArrayLength().ShouldBe(0);
        (await other.PostAsync($"/api/v1/partner/bookings/{code}/cancel", new { reason = "Not mine" })).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_venue_cancellation_refunds_everything_tells_the_player_and_frees_the_hour()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString()!;
        var (player, playerId) = await kit.PersonAsync(partner: false);
        var booked = (await player.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(4), 21))).Body.Data();
        var code = booked.GetProperty("code").GetString();

        (await partner.PostAsync($"/api/v1/partner/bookings/{code}/cancel", new { reason = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var cancelled = await partner.PostAsync($"/api/v1/partner/bookings/{code}/cancel", new { reason = "Floodlights repair" });
        cancelled.Status.ShouldBe(HttpStatusCode.OK, cancelled.Body.ToString());
        cancelled.Body.Data().GetProperty("status").GetString().ShouldBe("CancelledByVenue");
        cancelled.Body.Data().GetProperty("refundAmount").GetDecimal().ShouldBe(booked.GetProperty("total").GetDecimal());

        var told = await api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.UserId == playerId && n.Title == "The venue cancelled your booking" && n.Body!.Contains("Floodlights repair")));
        told.ShouldBeTrue();
        (await partner.PostAsync($"/api/v1/partner/bookings/{code}/cancel", new { reason = "Again" })).Body.Code().ShouldBe("wrong_status");
        var (another, _) = await kit.PersonAsync(partner: false);
        (await another.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(4), 21))).Status.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task A_no_show_is_marked_only_after_kick_off_and_accountants_cannot_do_it()
    {
        var (partner, _, venueId, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var code = (await player.PostAsync("/api/v1/bookings", Booking(pitch.GetProperty("slug").GetString()!, PartnerKit.Day(5), 18))).Body.Data().GetProperty("code").GetString()!;

        (await partner.PostAsync($"/api/v1/partner/bookings/{code}/no-show")).Body.Code().ShouldBe("not_started");
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == code).ExecuteUpdateAsync(u => u.SetProperty(b => b.Date, yesterday)));

        var (accountant, accountantId) = await kit.PersonAsync(partner: false);
        await kit.AddStaffAsync(venueId, accountantId, PartnerStaffRole.Accountant);
        var denied = await accountant.PostAsync($"/api/v1/partner/bookings/{code}/no-show");
        denied.Status.ShouldBe(HttpStatusCode.Forbidden);
        denied.Body.Code().ShouldBe("desk_only");
        (await accountant.GetAsync("/api/v1/partner/bookings?status=past")).Body.Data().GetArrayLength().ShouldBe(1);

        var (desk, deskId) = await kit.PersonAsync(partner: false);
        await kit.AddStaffAsync(venueId, deskId, PartnerStaffRole.CheckIn);
        (await desk.PostAsync($"/api/v1/partner/bookings/{code}/no-show")).Body.Data().GetProperty("status").GetString().ShouldBe("NoShow");
    }

    [Fact]
    public async Task The_export_is_an_excel_file_with_a_dashboard_sheet()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        await partner.PostAsync("/api/v1/partner/bookings/walk-in", new { pitchId = pitch.GetProperty("id").GetInt64(), date = PartnerKit.Day(6), hour = 10, duration = 1, name = "Walk-in team" });

        var response = await partner.Http.GetAsync("/api/v1/partner/bookings/export.xlsx?status=all");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        using var book = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        book.Worksheets.Select(w => w.Name).ShouldBe(["Dashboard", "Bookings"]);
        book.Worksheet("Dashboard").Cell(5, 1).GetString().ShouldBe("Bookings");
        book.Worksheet("Dashboard").Cell(5, 2).GetValue<int>().ShouldBe(1);
        book.Worksheet("Bookings").Cell(2, 6).GetString().ShouldBe("Walk-in team");
    }
}
