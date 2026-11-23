using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>
/// Owner's rule (2026-11-21): each pitch's owner chooses how players pay — a deposit in the app and the rest at the pitch,
/// or everything in the app with Super Malaab paying the venue after the match — and the admin sees each pitch's account.
/// </summary>
[Trait("Category", "Database")]
public sealed class PaymentModeTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private static object Booking(string slug, string date, int hour) => new { pitchSlug = slug, date, hour, duration = 1, payAtVenue = false, acceptWaiver = true };

    private static Dictionary<string, object?> WithMode(object pitch, string mode)
    {
        var fields = pitch.GetType().GetProperties().ToDictionary(p => p.Name, p => p.GetValue(pitch));
        fields["paymentMode"] = mode;
        return fields;
    }

    private async Task<(ApiClient Partner, Guid OwnerId, long PitchId, string Slug)> FullOnlinePitchAsync()
    {
        var (partner, ownerId, venueId, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = pitch.GetProperty("id").GetInt64();
        pitch.GetProperty("paymentMode").GetString().ShouldBe("Deposit");   // every pitch starts on the deposit rule
        var version = (await partner.GetAsync($"/api/v1/partner/pitches/{id}")).Body.Data().GetProperty("rowVersion").GetString();
        var edit = await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}", WithMode(PartnerKit.Pitch(venueId, rowVersion: version), "FullOnline"));
        edit.Status.ShouldBe(HttpStatusCode.OK, edit.Body.ToString());
        edit.Body.Data().GetProperty("paymentMode").GetString().ShouldBe("FullOnline");
        return (partner, ownerId, id, pitch.GetProperty("slug").GetString()!);
    }

    [Fact]
    public async Task The_owner_must_pick_one_of_the_payment_modes()
    {
        var (partner, _, venueId, pitch) = await kit.PartnerWithPitchAsync();
        var bad = await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{pitch.GetProperty("id").GetInt64()}",
            WithMode(PartnerKit.Pitch(venueId, rowVersion: pitch.GetProperty("rowVersion").GetString()), "HalfAndHalf"));
        bad.Status.ShouldBe(HttpStatusCode.BadRequest);
        bad.Body.HasFieldError("paymentMode").ShouldBeTrue();
    }

    [Fact]
    public async Task A_full_payment_pitch_takes_everything_in_the_app_and_leaves_nothing_at_the_pitch()
    {
        var (partner, ownerId, _, slug) = await FullOnlinePitchAsync();
        (await partner.GetAsync($"/api/v1/pitches/{slug}")).Body.Data().GetProperty("paymentMode").GetString().ShouldBe("FullOnline");
        var (player, playerId) = await kit.PersonAsync(partner: false);

        var hold = (await player.PostAsync("/api/v1/bookings/hold", new { pitchSlug = slug, date = PartnerKit.Day(4), hour = 19, duration = 1 })).Body.Data();
        hold.GetProperty("deposit").GetDecimal().ShouldBe(hold.GetProperty("total").GetDecimal());
        hold.GetProperty("balanceDue").GetDecimal().ShouldBe(0);

        var made = await player.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(4), 19));
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var booked = made.Body.Data();
        var total = booked.GetProperty("total").GetDecimal();
        booked.GetProperty("deposit").GetDecimal().ShouldBe(total);
        booked.GetProperty("balanceDue").GetDecimal().ShouldBe(0);
        (await api.DbAsync(db => db.Set<PointEntry>().Where(p => p.UserId == playerId).SumAsync(p => p.Points))).ShouldBe((int)total);

        // The venue's whole share is held by Super Malaab until after the match
        var held = await api.DbAsync(db => db.Set<Wallet>().Where(w => w.OwnerUserId == ownerId && w.Type == WalletType.PartnerBookings).Select(w => w.PendingBalance).SingleAsync());
        var pitchAmount = booked.GetProperty("pitchAmount").GetDecimal();
        held.ShouldBe(pitchAmount - Math.Round(pitchAmount * 0.10m, 2));
    }

    [Fact]
    public async Task A_half_deposit_pitch_takes_50_percent_in_the_app_and_the_rest_at_the_pitch()
    {
        var (partner, _, venueId, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var id = pitch.GetProperty("id").GetInt64();
        var version = (await partner.GetAsync($"/api/v1/partner/pitches/{id}")).Body.Data().GetProperty("rowVersion").GetString();
        var edit = await partner.SendAsync(HttpMethod.Put, $"/api/v1/partner/pitches/{id}", WithMode(PartnerKit.Pitch(venueId, rowVersion: version), "Deposit50"));
        edit.Status.ShouldBe(HttpStatusCode.OK, edit.Body.ToString());
        var slug = pitch.GetProperty("slug").GetString()!;
        var (player, _) = await kit.PersonAsync(partner: false);

        var made = await player.PostAsync("/api/v1/bookings", Booking(slug, PartnerKit.Day(5), 20));
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var booked = made.Body.Data();
        var total = booked.GetProperty("total").GetDecimal();
        booked.GetProperty("deposit").GetDecimal().ShouldBe(Math.Max(Math.Round(total / 2, 0, MidpointRounding.AwayFromZero), Math.Min(total, 100)));
        booked.GetProperty("balanceDue").GetDecimal().ShouldBe(total - booked.GetProperty("deposit").GetDecimal());
    }

    [Fact]
    public async Task The_admin_sees_each_pitch_account_and_can_export_it()
    {
        var (_, _, fullId, fullSlug) = await FullOnlinePitchAsync();
        var (_, _, _, depositPitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, _) = await kit.PersonAsync(partner: false);
        var full = (await player.PostAsync("/api/v1/bookings", Booking(fullSlug, PartnerKit.Day(5), 20))).Body.Data();
        var deposit = (await player.PostAsync("/api/v1/bookings", Booking(depositPitch.GetProperty("slug").GetString()!, PartnerKit.Day(5), 20))).Body.Data();

        var ceo = await api.CeoAsync();
        var range = $"from={PartnerKit.Day(0)}&to={PartnerKit.Day(10)}";
        var report = await ceo.GetAsync($"/api/v1/admin/settlements?{range}");
        report.Status.ShouldBe(HttpStatusCode.OK, report.Body.ToString());
        var rows = report.Body.Data().GetProperty("pitches").EnumerateArray().ToList();

        var f = rows.Single(r => r.GetProperty("pitchId").GetInt64() == fullId);
        f.GetProperty("paymentMode").GetString().ShouldBe("FullOnline");
        f.GetProperty("paidInApp").GetDecimal().ShouldBe(full.GetProperty("total").GetDecimal());
        f.GetProperty("cashAtPitch").GetDecimal().ShouldBe(0);
        (f.GetProperty("ourIncome").GetDecimal() + f.GetProperty("venueShareInApp").GetDecimal()).ShouldBe(full.GetProperty("total").GetDecimal());

        var d = rows.Single(r => r.GetProperty("pitchId").GetInt64() == depositPitch.GetProperty("id").GetInt64());
        d.GetProperty("paymentMode").GetString().ShouldBe("Deposit");
        d.GetProperty("paidInApp").GetDecimal().ShouldBe(deposit.GetProperty("deposit").GetDecimal());
        d.GetProperty("cashAtPitch").GetDecimal().ShouldBe(deposit.GetProperty("balanceDue").GetDecimal());
        d.GetProperty("stillAtPitch").GetDecimal().ShouldBe(deposit.GetProperty("balanceDue").GetDecimal());

        var partnerRow = report.Body.Data().GetProperty("partners").EnumerateArray().Single(p => p.GetProperty("partnerId").GetInt64() == f.GetProperty("partnerId").GetInt64());
        partnerRow.GetProperty("owed").GetDecimal().ShouldBe(f.GetProperty("venueShareInApp").GetDecimal());

        var file = await ceo.Http.GetAsync($"/api/v1/admin/settlements/export.xlsx?{range}");
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await player.GetAsync("/api/v1/admin/settlements")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
