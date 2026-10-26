using System.Net;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Tests.Api;

/// <summary>A partner withdraws; finance prepares, a second person approves (over the limit) and a different person sends.</summary>
[Trait("Category", "Database")]
public sealed class PayoutsTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    /// <summary>A finance team member with 2FA and the money permissions.</summary>
    private async Task<ApiClient> FinanceAsync(ApiClient ceo)
    {
        var email = $"fin.{Guid.NewGuid():N}"[..16] + "@superapp.test";
        var made = await ceo.PostAsync("/api/v1/ceo/admins", new { fullName = "Finance Person", email, roles = new[] { "Finance" }, limits = new { refund = 500, credit = 300, payout = 50000, canSecondApprove = true } });
        made.Status.ShouldBe(HttpStatusCode.Created, made.Body.ToString());
        var id = made.Body.Data().GetProperty("id").GetGuid();
        (await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/admins/{id}/permissions", new { permissions = new[] { "payouts.read", "payouts.prepare", "payouts.send", "finance.approve" } }))
            .Status.ShouldBe(HttpStatusCode.OK);
        var link = api.Emails.LinkTo(email, "auth/invite").ShouldNotBeNull();
        var token = HttpUtility.ParseQueryString(link.Fragment[(link.Fragment.IndexOf('?', StringComparison.Ordinal) + 1)..])["t"];
        (await (await api.ClientAsync()).PostAsync("/api/v1/auth/accept-invite", new { userId = id, token, password = "Finance2026" })).Status.ShouldBe(HttpStatusCode.OK);
        var client = await api.ClientAsync();
        await client.SignInAsync(email, "Finance2026");
        await client.EnableTwoFactorAsync();
        return client;
    }

    [Fact]
    public async Task A_withdrawal_needs_two_people_and_an_approval_over_the_limit()
    {
        var (partner, partnerId, _, _) = await kit.PartnerWithPitchAsync(live: true);
        using (var scope = api.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Set<Partner>().Where(p => p.UserId == partnerId).ExecuteUpdateAsync(u => u.SetProperty(p => p.PayoutDetail, "EG12 3456 7890").SetProperty(p => p.Status, PartnerStatus.Active));
            var wallet = new Wallet { OwnerUserId = partnerId, Type = WalletType.PartnerBookings, AvailableBalance = 30000 };
            db.Add(wallet);
            db.Add(new LedgerEntry { Wallet = wallet, Amount = 30000, Bucket = LedgerBucket.Available, Kind = LedgerKind.Adjustment, RefType = "Test", RefId = 1, GroupId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        (await partner.PostAsync("/api/v1/wallet/payouts", new { amount = 300 })).Body.Code().ShouldBe("under_minimum");
        (await partner.PostAsync("/api/v1/wallet/payouts", new { amount = 40000 })).Body.Code().ShouldBe("over_available");
        var asked = await partner.PostAsync("/api/v1/wallet/payouts", new { amount = 25000 });
        asked.Status.ShouldBe(HttpStatusCode.Created, asked.Body.ToString());
        var code = asked.Body.Data().GetProperty("code").GetString();
        (await partner.PostAsync("/api/v1/wallet/payouts", new { amount = 1000 })).Status.ShouldBe(HttpStatusCode.Conflict);
        (await partner.PostAsync($"/api/v1/admin/payouts/{code}/prepare")).Status.ShouldBe(HttpStatusCode.Forbidden);

        var ceo = await api.CeoAsync();
        var finance = await FinanceAsync(ceo);
        (await finance.PostAsync($"/api/v1/admin/payouts/{code}/prepare")).Body.Data().GetProperty("status").GetString().ShouldBe("WaitingForApproval");
        (await finance.PostAsync($"/api/v1/admin/payouts/{code}/send", new { transferRef = "TRX-1" })).Body.Code().ShouldBe("wrong_status");
        var list = await finance.GetAsync("/api/v1/admin/money-approvals");
        list.Status.ShouldBe(HttpStatusCode.OK, list.Body.ToString());
        var approval = list.Body.Data().EnumerateArray().Single(a => a.GetProperty("refCode").GetString() == code);
        var id = approval.GetProperty("id").GetInt64();
        (await finance.PostAsync($"/api/v1/admin/money-approvals/{id}/decide", new { decision = "approve" })).Body.Code().ShouldBe("two_people");
        (await ceo.PostAsync($"/api/v1/admin/money-approvals/{id}/decide", new { decision = "approve" })).Status.ShouldBe(HttpStatusCode.OK);

        (await finance.PostAsync($"/api/v1/admin/payouts/{code}/send", new { transferRef = "TRX-1" })).Body.Code().ShouldBe("two_people");
        var sent = await ceo.PostAsync($"/api/v1/admin/payouts/{code}/send", new { transferRef = "TRX-1" });
        sent.Body.Data().GetProperty("status").GetString().ShouldBe("Sent", sent.Body.ToString());

        var wallet2 = (await partner.GetAsync("/api/v1/wallet")).Body.Data().EnumerateArray().Single(w => w.GetProperty("type").GetString() == "PartnerBookings");
        wallet2.GetProperty("available").GetDecimal().ShouldBe(5000);
        wallet2.GetProperty("settled").GetDecimal().ShouldBe(25000);
        (await partner.GetAsync("/api/v1/receipts")).Body.Data().EnumerateArray().ShouldContain(r => r.GetProperty("kind").GetString() == "PayoutAdvice" && r.GetProperty("refCode").GetString() == code);
        var statement = await partner.Http.GetAsync("/api/v1/wallet/statement.xlsx");
        statement.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
