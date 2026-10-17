using System.Net;
using System.Web;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>Sign-up, sign-in, emailed links and the own account (API §2.1–2.2), with their edge cases.</summary>
[Trait("Category", "Database")]
public sealed class AuthTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private static int counter;

    /// <summary>A fresh, valid customer for each test (unique email and mobile number).</summary>
    private static object NewCustomer(out string email, out string phone, string password = "Football2026")
    {
        var n = Interlocked.Increment(ref counter);
        email = $"player{n}.{Guid.NewGuid():N}"[..24] + "@test.example";
        phone = $"+2010{Random.Shared.Next(10_000_000, 99_999_999)}";
        return new { fullName = "Test Player", email, phone, password, acceptTerms = true, language = "ar" };
    }

    private static (Guid User, string Token) LinkValues(Uri link)
    {
        var query = HttpUtility.ParseQueryString(link.Fragment[(link.Fragment.IndexOf('?', StringComparison.Ordinal) + 1)..]);
        return (Guid.Parse(query["u"]!), query["t"]!);
    }

    [Fact]
    public async Task Sign_up_creates_a_signed_in_customer_and_sends_a_confirmation_link_that_works()
    {
        var client = await api.ClientAsync();
        var (status, body) = await client.PostAsync("/api/v1/auth/sign-up", NewCustomer(out var email, out _));

        status.ShouldBe(HttpStatusCode.Created, body.ToString());
        body.Data().GetProperty("roles")[0].GetString().ShouldBe("Customer");
        body.Data().GetProperty("emailConfirmed").GetBoolean().ShouldBeFalse();

        await client.RefreshCsrfAsync();
        (await client.GetAsync("/api/v1/me")).Status.ShouldBe(HttpStatusCode.OK);

        var (user, token) = LinkValues(api.Emails.LinkTo(email, "auth/confirm").ShouldNotBeNull());
        (await client.PostAsync("/api/v1/auth/confirm-email", new { userId = user, token })).Status.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/v1/me")).Body.Data().GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Sign_up_refuses_a_used_email_or_mobile_number()
    {
        var client = await api.ClientAsync();
        var first = NewCustomer(out var email, out var phone);
        (await client.PostAsync("/api/v1/auth/sign-up", first)).Status.ShouldBe(HttpStatusCode.Created);

        var other = await api.ClientAsync();
        var sameEmail = await other.PostAsync("/api/v1/auth/sign-up", new { fullName = "Other", email, phone = "+201099999991", password = "Football2026", acceptTerms = true });
        var samePhone = await other.PostAsync("/api/v1/auth/sign-up", new { fullName = "Other", email = "other." + email, phone, password = "Football2026", acceptTerms = true });

        sameEmail.Status.ShouldBe(HttpStatusCode.Conflict);
        sameEmail.Body.Code().ShouldBe("email_taken");
        samePhone.Status.ShouldBe(HttpStatusCode.Conflict);
        samePhone.Body.Code().ShouldBe("phone_taken");
    }

    [Theory]
    [InlineData("phone", "0123")]
    [InlineData("password", "short1")]
    [InlineData("password", "nonumbers")]
    [InlineData("email", "not-an-email")]
    [InlineData("acceptTerms", null)]
    public async Task Sign_up_checks_every_field(string field, string? value)
    {
        var client = await api.ClientAsync();
        var payload = new Dictionary<string, object?>
        {
            ["fullName"] = "Test Player", ["email"] = "valid@test.example", ["phone"] = "+201012345678",
            ["password"] = "Football2026", ["acceptTerms"] = true,
        };
        payload[field] = field == "acceptTerms" ? false : value;

        var (status, body) = await client.PostAsync("/api/v1/auth/sign-up", payload);
        status.ShouldBe(HttpStatusCode.BadRequest);
        body.HasFieldError(field).ShouldBeTrue(body.ToString());
    }

    [Fact]
    public async Task A_change_without_the_csrf_header_is_refused()
    {
        var client = await api.ClientAsync();
        client.Http.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        var (status, body) = await client.PostAsync("/api/v1/auth/sign-in", new { login = "x@test.example", password = "whatever1" });

        status.ShouldBe(HttpStatusCode.BadRequest);
        body.Code().ShouldBe("csrf");
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_get_the_same_answer_and_five_tries_lock_the_account()
    {
        var setup = await api.ClientAsync();
        await setup.PostAsync("/api/v1/auth/sign-up", NewCustomer(out var email, out _));
        var client = await api.ClientAsync();

        var unknown = await client.PostAsync("/api/v1/auth/sign-in", new { login = "nobody@test.example", password = "Football2026" });
        var wrong = await client.PostAsync("/api/v1/auth/sign-in", new { login = email, password = "Wrong-pass-1" });
        unknown.Status.ShouldBe(HttpStatusCode.Unauthorized);
        wrong.Status.ShouldBe(HttpStatusCode.Unauthorized);
        unknown.Body.GetProperty("message").GetString().ShouldBe(wrong.Body.GetProperty("message").GetString());

        for (var i = 0; i < 4; i++)
        {
            await client.PostAsync("/api/v1/auth/sign-in", new { login = email, password = "Wrong-pass-1" });
        }
        var locked = await client.PostAsync("/api/v1/auth/sign-in", new { login = email, password = "Football2026" });
        locked.Status.ShouldBe(HttpStatusCode.Locked);
        locked.Body.Code().ShouldBe("locked_out");
    }

    [Fact]
    public async Task Sign_in_works_with_the_mobile_number_in_any_local_format()
    {
        var setup = await api.ClientAsync();
        await setup.PostAsync("/api/v1/auth/sign-up", NewCustomer(out _, out var phone));
        var client = await api.ClientAsync();

        var local = "0" + phone[3..];   // +2010xxxxxxxx → 010xxxxxxxx
        var body = await client.SignInAsync(local, "Football2026");
        body.Data().GetProperty("signedIn").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Me_needs_a_session()
    {
        var client = await api.ClientAsync();
        var (status, body) = await client.GetAsync("/api/v1/me");
        status.ShouldBe(HttpStatusCode.Unauthorized);
        body.Code().ShouldBe("unauthorized");
    }

    [Fact]
    public async Task Forgot_password_never_reveals_an_account_and_the_reset_link_works_once()
    {
        var setup = await api.ClientAsync();
        await setup.PostAsync("/api/v1/auth/sign-up", NewCustomer(out var email, out _));
        var client = await api.ClientAsync();

        (await client.PostAsync("/api/v1/auth/forgot-password", new { email = "nobody@test.example" })).Status.ShouldBe(HttpStatusCode.Accepted);
        (await client.PostAsync("/api/v1/auth/forgot-password", new { email })).Status.ShouldBe(HttpStatusCode.Accepted);
        var (user, token) = LinkValues(api.Emails.LinkTo(email, "auth/reset").ShouldNotBeNull());

        (await client.PostAsync("/api/v1/auth/reset-password", new { userId = user, token, newPassword = "NewPass2026" })).Status.ShouldBe(HttpStatusCode.OK);
        var reused = await client.PostAsync("/api/v1/auth/reset-password", new { userId = user, token, newPassword = "Another2026" });
        reused.Status.ShouldBe(HttpStatusCode.BadRequest);
        reused.Body.Code().ShouldBe("link_expired");

        (await client.PostAsync("/api/v1/auth/sign-in", new { login = email, password = "Football2026" })).Status.ShouldBe(HttpStatusCode.Unauthorized);
        await client.SignInAsync(email, "NewPass2026");
    }

    [Fact]
    public async Task Changing_the_password_checks_the_current_one()
    {
        var client = await api.ClientAsync();
        await client.PostAsync("/api/v1/auth/sign-up", NewCustomer(out var email, out _));
        await client.RefreshCsrfAsync();

        var wrong = await client.SendAsync(HttpMethod.Put, "/api/v1/me/password", new { currentPassword = "Wrong-pass-1", newPassword = "Changed2026" });
        wrong.Status.ShouldBe(HttpStatusCode.BadRequest);
        wrong.Body.HasFieldError("currentPassword").ShouldBeTrue();

        var ok = await client.SendAsync(HttpMethod.Put, "/api/v1/me/password", new { currentPassword = "Football2026", newPassword = "Changed2026" });
        ok.Status.ShouldBe(HttpStatusCode.NoContent);
        await (await api.ClientAsync()).SignInAsync(email, "Changed2026");
    }

    [Fact]
    public async Task Sign_out_everywhere_ends_the_other_sessions()
    {
        var setup = await api.ClientAsync();
        await setup.PostAsync("/api/v1/auth/sign-up", NewCustomer(out var email, out _));
        var phone = await api.ClientAsync();
        var laptop = await api.ClientAsync();
        await phone.SignInAsync(email, "Football2026");
        await laptop.SignInAsync(email, "Football2026");

        (await laptop.PostAsync("/api/v1/auth/sign-out-everywhere")).Status.ShouldBe(HttpStatusCode.NoContent);
        (await phone.GetAsync("/api/v1/me")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Updating_my_profile_normalises_the_phone_and_refuses_one_already_used()
    {
        var other = await api.ClientAsync();
        await other.PostAsync("/api/v1/auth/sign-up", NewCustomer(out _, out var takenPhone));
        var client = await api.ClientAsync();
        await client.PostAsync("/api/v1/auth/sign-up", NewCustomer(out _, out _));
        await client.RefreshCsrfAsync();

        var ok = await client.SendAsync(HttpMethod.Patch, "/api/v1/me", new { fullName = "New Name", phone = "01111111" + Random.Shared.Next(100, 999), language = "en" });
        ok.Status.ShouldBe(HttpStatusCode.OK, ok.Body.ToString());
        ok.Body.Data().GetProperty("phone").GetString()!.ShouldStartWith("+2011");

        var taken = await client.SendAsync(HttpMethod.Patch, "/api/v1/me", new { phone = takenPhone });
        taken.Status.ShouldBe(HttpStatusCode.Conflict);
        taken.Body.Code().ShouldBe("phone_taken");
    }

    [Fact]
    public async Task The_ceo_sees_every_permission_and_the_ceo_dashboard()
    {
        var client = await api.ClientAsync();
        var body = await client.SignInAsync(AccountsApiFactory.CeoEmail, AccountsApiFactory.CeoPassword);
        var me = body.Data().GetProperty("me");

        me.GetProperty("dashboards").EnumerateArray().Select(d => d.GetString()).ShouldContain("ceo");
        me.GetProperty("permissions").GetArrayLength().ShouldBe(SuperApp.Application.Security.Permissions.All.Count);
    }
}
