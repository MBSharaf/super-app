using System.Net;
using System.Text.Json;
using System.Web;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>Two-step sign-in, CEO-only endpoints and the CEO's team screen (API §2.1 2FA, §8 Admins).</summary>
[Trait("Category", "Database")]
public sealed class TeamTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private Task<ApiClient> CeoAsync() => api.CeoAsync();

    private static async Task<(Guid Id, string Email)> InviteAdminAsync(ApiClient ceo, string[] roles)
    {
        var email = $"admin.{Guid.NewGuid():N}"[..20] + "@superapp.test";
        var (status, body) = await ceo.PostAsync("/api/v1/ceo/admins", new { fullName = "Team Member", email, roles, limits = new { refund = 500, credit = 300, payout = 0, canSecondApprove = false } });
        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        return (body.Data().GetProperty("id").GetGuid(), email);
    }

    private async Task AcceptInviteAsync(Guid id, string email, string password)
    {
        var link = api.Emails.LinkTo(email, "auth/invite").ShouldNotBeNull();
        var token = HttpUtility.ParseQueryString(link.Fragment[(link.Fragment.IndexOf('?', StringComparison.Ordinal) + 1)..])["t"];
        var client = await api.ClientAsync();
        (await client.PostAsync("/api/v1/auth/accept-invite", new { userId = id, token, password })).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ceo_endpoints_refuse_guests_and_customers()
    {
        var guest = await api.ClientAsync();
        (await guest.GetAsync("/api/v1/ceo/admins")).Status.ShouldBe(HttpStatusCode.Unauthorized);

        var customer = await api.ClientAsync();
        await customer.PostAsync("/api/v1/auth/sign-up", new { fullName = "Customer", email = $"c{Guid.NewGuid():N}"[..12] + "@test.example", phone = "+2011" + Random.Shared.Next(10_000_000, 99_999_999), password = "Football2026", acceptTerms = true });
        await customer.RefreshCsrfAsync();
        var asCustomer = await customer.GetAsync("/api/v1/ceo/admins");
        asCustomer.Status.ShouldBe(HttpStatusCode.Forbidden);
        asCustomer.Body.Code().ShouldBe("forbidden");
    }

    [Fact]
    public async Task A_new_admin_is_invited_sets_a_password_and_must_set_up_2fa()
    {
        var ceo = await CeoAsync();
        var (id, email) = await InviteAdminAsync(ceo, ["Support"]);
        await AcceptInviteAsync(id, email, "Support2026");

        var admin = await api.ClientAsync();
        var me = (await admin.SignInAsync(email, "Support2026")).Data().GetProperty("me");
        me.GetProperty("requires2faSetup").GetBoolean().ShouldBeTrue();
        me.GetProperty("dashboards").EnumerateArray().Select(d => d.GetString()).ShouldContain("admin");
        me.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ShouldContain("support.reply");

        var notCeo = await admin.GetAsync("/api/v1/ceo/admins");
        notCeo.Status.ShouldBe(HttpStatusCode.Forbidden);
        notCeo.Body.Code().ShouldBe("forbidden");
    }

    [Fact]
    public async Task An_invitation_works_once_and_refuses_bad_input()
    {
        var ceo = await CeoAsync();
        var (id, email) = await InviteAdminAsync(ceo, ["Content"]);
        await AcceptInviteAsync(id, email, "Content2026");

        var link = api.Emails.LinkTo(email, "auth/invite")!;
        var token = HttpUtility.ParseQueryString(link.Fragment[(link.Fragment.IndexOf('?', StringComparison.Ordinal) + 1)..])["t"];
        var again = await (await api.ClientAsync()).PostAsync("/api/v1/auth/accept-invite", new { userId = id, token, password = "Another2026" });
        again.Status.ShouldBe(HttpStatusCode.BadRequest);
        again.Body.Code().ShouldBe("link_expired");

        var badRole = await ceo.PostAsync("/api/v1/ceo/admins", new { fullName = "X Person", email = "x.person@superapp.test", roles = new[] { "CEO" } });
        badRole.Status.ShouldBe(HttpStatusCode.BadRequest);
        badRole.Body.HasFieldError("roles").ShouldBeTrue();
        var duplicate = await ceo.PostAsync("/api/v1/ceo/admins", new { fullName = "Again", email, roles = new[] { "Support" } });
        duplicate.Status.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task The_ceo_grants_approvals_to_one_admin_and_sets_money_limits()
    {
        var ceo = await CeoAsync();
        var (id, _) = await InviteAdminAsync(ceo, ["Support"]);

        var granted = await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/admins/{id}/permissions", new { permissions = new[] { "approvals.partners", "approvals.sellers" } });
        granted.Status.ShouldBe(HttpStatusCode.OK, granted.Body.ToString());
        var all = granted.Body.Data().GetProperty("allPermissions").EnumerateArray().Select(p => p.GetString()).ToList();
        all.ShouldContain("approvals.partners");
        all.ShouldContain("support.reply");

        var unknown = await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/admins/{id}/permissions", new { permissions = new[] { "everything.please" } });
        unknown.Status.ShouldBe(HttpStatusCode.BadRequest);

        var limits = await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/admins/{id}/limits", new { refund = 2000, credit = 1000, payout = 5000, canSecondApprove = true });
        limits.Body.Data().GetProperty("limits").GetProperty("payout").GetDecimal().ShouldBe(5000);
        (await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/admins/{id}/limits", new { refund = -1, credit = 0, payout = 0, canSecondApprove = false })).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Suspending_an_admin_ends_the_session_and_blocks_sign_in_until_reactivated()
    {
        var ceo = await CeoAsync();
        var (id, email) = await InviteAdminAsync(ceo, ["Moderator"]);
        await AcceptInviteAsync(id, email, "Moderate2026");
        var admin = await api.ClientAsync();
        await admin.SignInAsync(email, "Moderate2026");

        (await ceo.PostAsync($"/api/v1/ceo/admins/{id}/suspend", new { reason = "Left the company" })).Status.ShouldBe(HttpStatusCode.OK);
        (await admin.GetAsync("/api/v1/me")).Status.ShouldBe(HttpStatusCode.Unauthorized);
        var blocked = await (await api.ClientAsync()).PostAsync("/api/v1/auth/sign-in", new { login = email, password = "Moderate2026" });
        blocked.Status.ShouldBe(HttpStatusCode.Forbidden);
        blocked.Body.Code().ShouldBe("account_suspended");

        (await ceo.PostAsync($"/api/v1/ceo/admins/{id}/reactivate")).Status.ShouldBe(HttpStatusCode.OK);
        await (await api.ClientAsync()).SignInAsync(email, "Moderate2026");
    }

    [Fact]
    public async Task The_ceo_account_is_protected_and_suspending_needs_a_reason()
    {
        var ceo = await CeoAsync();
        var me = (await ceo.GetAsync("/api/v1/me")).Body.Data().GetProperty("id").GetGuid();
        var self = await ceo.PostAsync($"/api/v1/ceo/admins/{me}/suspend", new { reason = "Testing" });
        self.Status.ShouldBe(HttpStatusCode.Forbidden);
        self.Body.Code().ShouldBe("ceo_protected");

        var (id, _) = await InviteAdminAsync(ceo, ["Support"]);
        (await ceo.PostAsync($"/api/v1/ceo/admins/{id}/suspend", new { reason = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.GetAsync($"/api/v1/ceo/admins/{Guid.NewGuid()}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Two_step_sign_in_checks_the_code_and_accepts_a_recovery_code_once()
    {
        await (await CeoAsync()).GetAsync("/api/v1/me");   // makes sure the CEO's authenticator is on
        var client = await api.ClientAsync();
        var first = await client.SignInAsync(AccountsApiFactory.CeoEmail, AccountsApiFactory.CeoPassword);
        first.Data().GetProperty("requires2fa").GetBoolean().ShouldBeTrue();
        (await client.GetAsync("/api/v1/me")).Status.ShouldBe(HttpStatusCode.Unauthorized);

        var wrong = await client.PostAsync("/api/v1/auth/2fa/verify", new { code = "000000" });
        wrong.Status.ShouldBe(HttpStatusCode.Unauthorized);
        wrong.Body.Code().ShouldBe("wrong_code");

        var codes = await NewRecoveryCodesAsync();
        (await client.PostAsync("/api/v1/auth/2fa/verify", new { code = codes[0], isRecoveryCode = true })).Status.ShouldBe(HttpStatusCode.OK);
        await client.RefreshCsrfAsync();
        (await client.GetAsync("/api/v1/ceo/admins")).Status.ShouldBe(HttpStatusCode.OK);

        var other = await api.ClientAsync();
        await other.SignInAsync(AccountsApiFactory.CeoEmail, AccountsApiFactory.CeoPassword);
        (await other.PostAsync("/api/v1/auth/2fa/verify", new { code = codes[0], isRecoveryCode = true })).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Team_accounts_cannot_turn_2fa_off_and_the_ceo_sees_roles_and_permissions()
    {
        var ceo = await CeoAsync();
        var off = await ceo.PostAsync("/api/v1/auth/2fa/disable", new { code = Totp.Code(api.CeoKey!) });
        off.Status.ShouldBe(HttpStatusCode.Forbidden);
        off.Body.Code().ShouldBe("2fa_required_for_staff");

        var roles = (await ceo.GetAsync("/api/v1/ceo/roles")).Body.Data().EnumerateArray().ToList();
        roles.Single(r => r.GetProperty("name").GetString() == "CEO").GetProperty("permissions").GetArrayLength().ShouldBe(SuperApp.Application.Security.Permissions.All.Count);
        (await ceo.GetAsync("/api/v1/ceo/permissions")).Body.Data().GetArrayLength().ShouldBe(SuperApp.Application.Security.Permissions.All.Count);
    }

    private async Task<List<string>> NewRecoveryCodesAsync()
    {
        var ceo = await CeoAsync();
        var body = (await ceo.PostAsync("/api/v1/auth/2fa/recovery-codes", new { code = Totp.Code(api.CeoKey!) })).Body;
        return body.Data().GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToList();
    }
}
