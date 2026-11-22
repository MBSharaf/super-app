using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;
using SuperApp.Application.Abstractions.Identity;

namespace SuperApp.Tests.Api;

/// <summary>The API with both providers switched on; the provider checks are replaced by a fake that reads the test token.</summary>
public sealed class ExternalLoginApiFactory : AccountsApiFactory
{
    public const string GoogleClientId = "test-client.apps.googleusercontent.com";
    public const string FacebookAppId = "1234567890";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ExternalLogins:Google:ClientId", GoogleClientId);
        builder.UseSetting("ExternalLogins:Facebook:AppId", FacebookAppId);
        builder.UseSetting("ExternalLogins:Facebook:AppSecret", "test-secret-never-sent");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IExternalLoginVerifier>();
            services.AddScoped<IExternalLoginVerifier>(_ => new FakeVerifier(ExternalProviders.Google));
            services.AddScoped<IExternalLoginVerifier>(_ => new FakeVerifier(ExternalProviders.Facebook));
        });
    }

    /// <summary>A test token: "ok|key|email|verified|name"; anything else is refused, like a forged token.</summary>
    public static string Token(string key, string email, bool verified = true, string name = "Test Player") => $"ok|{key}|{email}|{verified}|{name}";

    private sealed class FakeVerifier(string provider) : IExternalLoginVerifier
    {
        public string Provider => provider;

        public Task<ExternalProfile?> VerifyAsync(string token, CancellationToken ct)
        {
            var p = token.Split('|');
            return Task.FromResult(p.Length == 5 && p[0] == "ok" ? new ExternalProfile(provider, p[1], p[2], bool.Parse(p[3]), p[4]) : null);
        }
    }
}

/// <summary>"Continue with Google / Facebook" (API §2.1): new accounts, linking, refusals.</summary>
[Trait("Category", "Database")]
public sealed class ExternalLoginTests(ExternalLoginApiFactory api) : IClassFixture<ExternalLoginApiFactory>
{
    private static string NewEmail() => $"ext.{Guid.NewGuid():N}"[..20] + "@test.example";

    private static object Body(string provider, string token, bool acceptTerms = true) => new { provider, token, acceptTerms, remember = true, language = "ar" };

    [Fact]
    public async Task Providers_lists_the_public_ids_only()
    {
        var (status, body) = await (await api.ClientAsync()).GetAsync("/api/v1/auth/providers");

        status.ShouldBe(HttpStatusCode.OK);
        body.Data().GetProperty("googleClientId").GetString().ShouldBe(ExternalLoginApiFactory.GoogleClientId);
        body.Data().GetProperty("facebookAppId").GetString().ShouldBe(ExternalLoginApiFactory.FacebookAppId);
        body.ToString().ShouldNotContain("test-secret");
    }

    [Fact]
    public async Task A_first_Google_sign_in_needs_the_terms_then_opens_a_confirmed_customer_account()
    {
        var client = await api.ClientAsync();
        var email = NewEmail();
        var token = ExternalLoginApiFactory.Token("g-" + Guid.NewGuid().ToString("N"), email, name: "Omar Test");

        var refused = await client.PostAsync("/api/v1/auth/external", Body("Google", token, acceptTerms: false));
        refused.Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        refused.Body.Code().ShouldBe("terms_required");

        var (status, body) = await client.PostAsync("/api/v1/auth/external", Body("Google", token));
        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        var me = body.Data().GetProperty("me");
        me.GetProperty("email").GetString().ShouldBe(email);
        me.GetProperty("fullName").GetString().ShouldBe("Omar Test");
        me.GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
        me.GetProperty("roles")[0].GetString().ShouldBe("Customer");

        await client.RefreshCsrfAsync();
        (await client.GetAsync("/api/v1/me")).Status.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsync("/api/v1/auth/sign-out")).Status.ShouldBe(HttpStatusCode.NoContent);
        await client.RefreshCsrfAsync();
        (await client.GetAsync("/api/v1/me")).Status.ShouldBe(HttpStatusCode.Unauthorized);

        var again = await (await api.ClientAsync()).PostAsync("/api/v1/auth/external", Body("Google", token, acceptTerms: false));
        again.Status.ShouldBe(HttpStatusCode.OK);
        again.Body.Data().GetProperty("me").GetProperty("id").GetString().ShouldBe(me.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Facebook_with_the_email_of_a_password_account_links_to_that_account_and_the_password_still_works()
    {
        var email = NewEmail();
        var signUp = await (await api.ClientAsync()).PostAsync("/api/v1/auth/sign-up",
            new { fullName = "Password Player", email, phone = $"+2011{Random.Shared.Next(10_000_000, 99_999_999)}", password = "Football2026", acceptTerms = true });
        signUp.Status.ShouldBe(HttpStatusCode.Created);

        var (status, body) = await (await api.ClientAsync()).PostAsync("/api/v1/auth/external",
            Body("Facebook", ExternalLoginApiFactory.Token("fb-" + Guid.NewGuid().ToString("N"), email), acceptTerms: false));

        status.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.Data().GetProperty("me").GetProperty("id").GetString().ShouldBe(signUp.Body.Data().GetProperty("id").GetString());
        body.Data().GetProperty("me").GetProperty("emailConfirmed").GetBoolean().ShouldBeTrue();
        (await (await api.ClientAsync()).SignInAsync(email, "Football2026")).Data().GetProperty("signedIn").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("forged-token", HttpStatusCode.Unauthorized, "external_failed")]
    [InlineData("unverified", (HttpStatusCode)422, "external_no_email")]
    [InlineData("no-email", (HttpStatusCode)422, "external_no_email")]
    public async Task Bad_or_incomplete_provider_answers_are_refused(string kind, HttpStatusCode expected, string code)
    {
        var token = kind switch
        {
            "unverified" => ExternalLoginApiFactory.Token("g-x1", NewEmail(), verified: false),
            "no-email" => ExternalLoginApiFactory.Token("g-x2", "", verified: false),
            _ => "eyJhbGciOiJub25lIn0.e30.",
        };
        var (status, body) = await (await api.ClientAsync()).PostAsync("/api/v1/auth/external", Body("Google", token));

        status.ShouldBe(expected, body.ToString());
        body.Code().ShouldBe(code);
    }

    [Fact]
    public async Task Team_accounts_cannot_skip_their_password_and_authenticator()
    {
        await api.CeoAsync();
        var (status, body) = await (await api.ClientAsync()).PostAsync("/api/v1/auth/external",
            Body("Google", ExternalLoginApiFactory.Token("g-ceo", AccountsApiFactory.CeoEmail)));

        status.ShouldBe(HttpStatusCode.Forbidden, body.ToString());
        body.Code().ShouldBe("staff_password_only");
    }

    [Fact]
    public async Task An_unknown_provider_is_a_validation_error()
    {
        var (status, _) = await (await api.ClientAsync()).PostAsync("/api/v1/auth/external", Body("Twitter", "x"));
        status.ShouldBe(HttpStatusCode.BadRequest);
    }
}
