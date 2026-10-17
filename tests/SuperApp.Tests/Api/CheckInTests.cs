using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>QR check-in (API §3.6): every rejection reason, repeated scans, and who may scan.</summary>
[Trait("Category", "Database")]
public sealed class CheckInTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");
    private readonly PartnerKit kit = new(api);

    private static DateTime CairoNow => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Cairo);

    /// <summary>A live pitch with one app booking; returns the partner, the pitch id, the booking code, its QR token and the player.</summary>
    private async Task<(ApiClient Partner, long PitchId, string Code, string Token, Guid PlayerId)> BookingAsync(int daysAhead = 5)
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var (player, playerId) = await kit.PersonAsync(partner: false);
        var booked = (await player.PostAsync("/api/v1/bookings", new { pitchSlug = pitch.GetProperty("slug").GetString(), date = PartnerKit.Day(daysAhead), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true })).Body.Data();
        return (partner, pitch.GetProperty("id").GetInt64(), booked.GetProperty("code").GetString()!, booked.GetProperty("qrToken").GetString()!, playerId);
    }

    /// <summary>Moves the booking to start at a Cairo hour today (offset from now).</summary>
    private Task<int> MoveAsync(string code, int hoursFromNow)
    {
        var start = CairoNow.AddHours(hoursFromNow);
        return api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == code)
            .ExecuteUpdateAsync(u => u.SetProperty(b => b.Date, DateOnly.FromDateTime(start)).SetProperty(b => b.StartHour, (byte)start.Hour)));
    }

    private static async Task<JsonElement> ScanAsync(ApiClient client, long? pitchId, string? token = null, string? code = null, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/check-in/scan") { Content = JsonContent.Create(new { token, code, pitchId, device = "Front desk" }) };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        var response = await client.Http.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Data();
    }

    [Fact]
    public async Task A_valid_qr_checks_in_once_and_a_repeated_request_gets_the_same_answer()
    {
        var (partner, pitchId, code, token, playerId) = await BookingAsync();
        await MoveAsync(code, 0);

        var first = await ScanAsync(partner, pitchId, token, key: "scan-key-1");
        first.GetProperty("result").GetString().ShouldBe("accepted");
        first.GetProperty("code").GetString().ShouldBe(code);
        (await ScanAsync(partner, pitchId, token, key: "scan-key-1")).GetProperty("result").GetString().ShouldBe("accepted");
        var again = await ScanAsync(partner, null, token);
        again.GetProperty("reason").GetString().ShouldBe("already_used");

        (await api.DbAsync(db => db.Set<QrScan>().CountAsync(s => s.Booking!.Code == code))).ShouldBe(2);
        (await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == code).Select(b => b.Status).SingleAsync())).ShouldBe(BookingStatus.CheckedIn);
        (await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Notifications.Notification>().AnyAsync(n => n.UserId == playerId && n.Title.StartsWith("Checked in at")))).ShouldBeTrue();
        var history = (await partner.GetAsync($"/api/v1/check-in/history?pitchId={pitchId}")).Body.Data();
        history.GetArrayLength().ShouldBe(2);
        history[0].GetProperty("device").GetString().ShouldBe("Front desk");
    }

    [Fact]
    public async Task Each_rejection_has_its_reason()
    {
        var (partner, pitchId, code, token, playerId) = await BookingAsync();

        (await ScanAsync(partner, pitchId, token + "x")).GetProperty("reason").GetString().ShouldBe("bad_signature");
        (await ScanAsync(partner, pitchId, code: "SA-NOPE0000")).GetProperty("reason").GetString().ShouldBe("bad_signature");
        (await ScanAsync(partner, pitchId, token)).GetProperty("reason").GetString().ShouldBe("wrong_day");

        var hour = CairoNow.Hour;
        await MoveAsync(code, hour >= 3 ? -3 : 3);
        (await ScanAsync(partner, pitchId, code: code.ToLowerInvariant())).GetProperty("reason").GetString().ShouldBe("wrong_hour");

        await MoveAsync(code, 0);
        await api.DbAsync(db => db.Users.Where(u => u.Id == playerId).ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, UserStatus.BlockedFromBookingAndOrdering)));
        (await ScanAsync(partner, pitchId, token)).GetProperty("reason").GetString().ShouldBe("account_blocked");

        await api.DbAsync(db => db.Set<Booking>().Where(b => b.Code == code).ExecuteUpdateAsync(u => u.SetProperty(b => b.Status, BookingStatus.CancelledByCustomer)));
        (await ScanAsync(partner, pitchId, token)).GetProperty("reason").GetString().ShouldBe("cancelled");
    }

    [Fact]
    public async Task Another_venue_cannot_check_in_the_booking()
    {
        var (_, pitchId, code, token, _) = await BookingAsync();
        await MoveAsync(code, 0);
        var (other, _, _, otherPitch) = await kit.PartnerWithPitchAsync(live: true);

        (await ScanAsync(other, otherPitch.GetProperty("id").GetInt64(), token)).GetProperty("reason").GetString().ShouldBe("other_venue");
        (await ScanAsync(other, null, token)).GetProperty("reason").GetString().ShouldBe("other_venue");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/check-in/scan") { Content = JsonContent.Create(new { token, pitchId }) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await other.Http.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var noKey = await other.PostAsync("/api/v1/check-in/scan", new { token, pitchId = otherPitch.GetProperty("id").GetInt64() });
        noKey.Status.ShouldBe(HttpStatusCode.BadRequest);
        noKey.Body.Code().ShouldBe("idempotency_key");
    }
}
