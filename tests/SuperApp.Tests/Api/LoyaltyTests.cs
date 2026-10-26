using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Tests.Api;

/// <summary>Venue codes and Super Points vouchers take money off a booking; points turn into vouchers.</summary>
[Trait("Category", "Database")]
public sealed class LoyaltyTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task A_venue_code_takes_its_discount_off_the_booking()
    {
        var (partner, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString();
        var code = "GOAL" + Random.Shared.Next(1000, 9999);
        var body = new { code, discountType = "Percent", value = 20, minSpend = 0, maxUses = 10, perCustomer = 1, startsOn = PartnerKit.Day(-1), endsOn = (string?)null, label = "Opening", pitchIds = Array.Empty<long>() };
        (await partner.PostAsync("/api/v1/partner/codes", body)).Status.ShouldBe(HttpStatusCode.Created);
        (await partner.PostAsync("/api/v1/partner/codes", body)).Status.ShouldBe(HttpStatusCode.Conflict);

        var (player, _) = await kit.PersonAsync(partner: false);
        var check = (await player.PostAsync("/api/v1/codes/check", new { code = code.ToLowerInvariant(), pitchSlug = slug, total = 500 })).Body.Data();
        check.GetProperty("valid").GetBoolean().ShouldBeTrue();
        check.GetProperty("discount").GetDecimal().ShouldBe(100);
        (await player.PostAsync("/api/v1/codes/check", new { code = "NOPE123", pitchSlug = slug, total = 500 })).Body.Data().GetProperty("valid").GetBoolean().ShouldBeFalse();

        var made = await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(5), hour = 20, duration = 1, payAtVenue = false, acceptWaiver = true, promoCode = code });
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var b = made.Body.Data();
        b.GetProperty("total").GetDecimal().ShouldBe(b.GetProperty("pitchAmount").GetDecimal() * 0.8m + b.GetProperty("serviceFee").GetDecimal());
        var again = await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(5), hour = 21, duration = 1, payAtVenue = false, acceptWaiver = true, promoCode = code });
        again.Body.Code().ShouldBe("code_used_by_you");
    }

    [Fact]
    public async Task Points_become_a_voucher_that_pays_part_of_a_booking_once()
    {
        var (_, _, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var slug = pitch.GetProperty("slug").GetString();
        var (player, playerId) = await kit.PersonAsync(partner: false);
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Add(new PointEntry { UserId = playerId, Points = 3000, Kind = PointKind.AdminAdjustment, Reason = "Test", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        (await player.PostAsync("/api/v1/points/redeem", new { amount = 150 })).Body.Code().ShouldBe("not_multiple");
        (await player.PostAsync("/api/v1/points/redeem", new { amount = 200 })).Body.Code().ShouldBe("not_enough_points");
        var voucher = await player.PostAsync("/api/v1/points/redeem", new { amount = 100 });
        voucher.Status.ShouldBe(HttpStatusCode.Created, voucher.Body.ToString());
        var code = voucher.Body.Data().GetProperty("code").GetString()!;
        (await player.GetAsync("/api/v1/points")).Body.Data().GetProperty("balance").GetInt32().ShouldBe(1000);

        var made = (await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(6), hour = 18, duration = 1, payAtVenue = false, acceptWaiver = true, voucherCode = code })).Body.Data();
        made.GetProperty("total").GetDecimal().ShouldBe(Math.Max(0, made.GetProperty("pitchAmount").GetDecimal() + made.GetProperty("serviceFee").GetDecimal() - 100));
        (await player.GetAsync("/api/v1/vouchers/mine")).Body.Data()[0].GetProperty("status").GetString().ShouldBe("Used");
        (await player.PostAsync("/api/v1/bookings", new { pitchSlug = slug, date = PartnerKit.Day(6), hour = 19, duration = 1, payAtVenue = false, acceptWaiver = true, voucherCode = code }))
            .Body.Code().ShouldBe("voucher_used");
    }
}
