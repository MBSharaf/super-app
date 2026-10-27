using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Tests.Api;

/// <summary>The admin drawers' money actions (backend R5): wallet credit, Super Points, refusals, commission, balance, closing a pitch.</summary>
[Trait("Category", "Database")]
public sealed class AdminMoneyActionsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task A_customer_gets_a_wallet_credit_points_and_a_refusals_reset()
    {
        var (player, id) = await kit.PersonAsync(partner: false);
        var ceo = await api.CeoAsync();
        var path = $"/api/v1/admin/customers/{id}";

        var credit = await ceo.PostAsync($"{path}/credit", new { amount = 150, reason = "Sorry for the closed pitch" });
        credit.Status.ShouldBe(HttpStatusCode.OK, credit.Body.ToString());
        credit.Body.Data().GetProperty("walletBalance").GetDecimal().ShouldBe(150);
        credit.Body.Data().GetProperty("credits")[0].GetProperty("reason").GetString().ShouldBe("Sorry for the closed pitch");
        (await ceo.PostAsync($"{path}/credit", new { amount = 9000, reason = "Too much" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await player.PostAsync($"{path}/credit", new { amount = 50, reason = "Myself" })).Status.ShouldBe(HttpStatusCode.Forbidden);

        (await ceo.PostAsync($"{path}/points", new { points = 200, reason = "Welcome gift" })).Body.Data().GetProperty("points").GetInt32().ShouldBe(200);
        var tooMany = await ceo.PostAsync($"{path}/points", new { points = -500, reason = "Mistake" });
        tooMany.Body.Code().ShouldBe("not_enough_points");
        (await ceo.PostAsync($"{path}/points", new { points = -50, reason = "Mistake" })).Body.Data().GetProperty("points").GetInt32().ShouldBe(150);

        (await ceo.PostAsync($"{path}/refusals/reset")).Body.Code().ShouldBe("no_refusals");
        await api.DbAsync(db => db.Users.Where(u => u.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.CodRefusals, 2)));
        var reset = await ceo.PostAsync($"{path}/refusals/reset");
        reset.Body.Data().GetProperty("customer").GetProperty("codRefusals").GetInt32().ShouldBe(0);
        reset.Body.Data().GetProperty("customer").GetProperty("spent").GetDecimal().ShouldBe(0);
    }

    [Fact]
    public async Task A_partner_gets_a_commission_deal_a_balance_adjustment_and_a_pitch_closed_and_reopened()
    {
        var (_, userId, _, pitch) = await kit.PartnerWithPitchAsync(live: true);
        var partnerId = await api.DbAsync(db => db.Set<Partner>().Where(p => p.UserId == userId).Select(p => p.Id).SingleAsync());
        var ceo = await api.CeoAsync();
        var path = $"/api/v1/admin/partners/{partnerId}";

        var deal = await ceo.SendAsync(HttpMethod.Put, $"{path}/commission", new { bookings = 8, store = (decimal?)null, note = "12-month deal" });
        deal.Status.ShouldBe(HttpStatusCode.OK, deal.Body.ToString());
        deal.Body.Data().GetProperty("partner").GetProperty("bookingCommission").GetDecimal().ShouldBe(8);
        (await ceo.SendAsync(HttpMethod.Put, $"{path}/commission", new { bookings = 70 })).Status.ShouldBe(HttpStatusCode.BadRequest);

        (await ceo.PostAsync($"{path}/adjust", new { amount = 300, reason = "Missed payout" })).Body.Data().GetProperty("partner").GetProperty("balance").GetDecimal().ShouldBe(300);
        (await ceo.PostAsync($"{path}/adjust", new { amount = -500, reason = "Too much" })).Body.Code().ShouldBe("not_enough_balance");
        (await ceo.PostAsync($"{path}/adjust", new { amount = -100, reason = "Double credit" })).Body.Data().GetProperty("partner").GetProperty("balance").GetDecimal().ShouldBe(200);

        var pitchId = pitch.GetProperty("id").GetInt64();
        var closed = await ceo.PostAsync($"/api/v1/admin/pitches/{pitchId}/close", new { reason = "Safety check after a complaint" });
        closed.Status.ShouldBe(HttpStatusCode.OK, closed.Body.ToString());
        closed.Body.Data().GetProperty("partner").GetProperty("closedPitches").GetInt32().ShouldBe(1);
        closed.Body.Data().GetProperty("pitchList")[0].GetProperty("status").GetString().ShouldBe("ClosedByAdmin");
        (await ceo.PostAsync($"/api/v1/admin/pitches/{pitchId}/close", new { reason = "Again" })).Body.Code().ShouldBe("wrong_status");
        (await ceo.PostAsync($"/api/v1/admin/pitches/{pitchId}/reopen")).Body.Data().GetProperty("pitchList")[0].GetProperty("status").GetString().ShouldBe("Live");
    }

    [Fact]
    public async Task Money_actions_respect_the_admins_limits()
    {
        var (_, id) = await kit.PersonAsync(partner: false);
        var ceo = await api.CeoAsync();
        var email = $"fin.{Guid.NewGuid():N}"[..20] + "@superapp.test";
        var invited = await ceo.PostAsync("/api/v1/ceo/admins", new { fullName = "Finance Person", email, roles = new[] { "Finance" }, limits = new { refund = 0, credit = 100, payout = 0, canSecondApprove = false } });
        invited.Status.ShouldBe(HttpStatusCode.Created, invited.Body.ToString());
        var link = api.Emails.LinkTo(email, "auth/invite").ShouldNotBeNull();
        var token = System.Web.HttpUtility.ParseQueryString(link.Fragment[(link.Fragment.IndexOf('?', StringComparison.Ordinal) + 1)..])["t"];
        (await (await api.ClientAsync()).PostAsync("/api/v1/auth/accept-invite", new { userId = invited.Body.Data().GetProperty("id").GetGuid(), token, password = "Finance2026" })).Status.ShouldBe(HttpStatusCode.OK);
        var signedIn = await api.ClientAsync();
        await signedIn.SignInAsync(email, "Finance2026");
        await signedIn.EnableTwoFactorAsync();
        var over = await signedIn.PostAsync($"/api/v1/admin/customers/{id}/credit", new { amount = 150, reason = "Goodwill" });
        over.Status.ShouldBe(HttpStatusCode.Forbidden);
        over.Body.Code().ShouldBe("over_credit_limit");
        (await signedIn.PostAsync($"/api/v1/admin/customers/{id}/credit", new { amount = 80, reason = "Goodwill" })).Status.ShouldBe(HttpStatusCode.OK);
    }
}
