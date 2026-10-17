using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using SuperApp.Application.Common.Responses;
using SuperApp.Infrastructure.Identity;

namespace SuperApp.Api.Security;

/// <summary>
/// Sign-in with a secure Identity cookie (decision: cookie, not tokens) plus the anti-forgery header the site sends
/// with every change. API calls never get redirected to a login page: they get 401 / 403 in the unified shape.
/// </summary>
public static class CookieAuthSetup
{
    public const string CookieName = "sa.auth";
    public const string CsrfHeader = "X-CSRF-TOKEN";

    public static IServiceCollection AddSuperAppAuth(this IServiceCollection services)
    {
        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();
        services.ConfigureApplicationCookie(ConfigureCookie);
        services.AddSuperAppAuthorization();
        services.AddAntiforgery(o =>
        {
            o.HeaderName = CsrfHeader;
            o.Cookie.Name = "sa.csrf";
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Strict;
        });
        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions o)
    {
        o.Cookie.Name = CookieName;
        o.Cookie.HttpOnly = true;
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromDays(14);
        o.SlidingExpiration = true;
        o.Events.OnValidatePrincipal = SessionRules.ValidatePrincipalAsync;
        o.Events.OnRedirectToLogin = ctx => Deny(ctx.HttpContext, StatusCodes.Status401Unauthorized, "Please sign in first.", "unauthorized");
        o.Events.OnRedirectToAccessDenied = ctx => Deny(ctx.HttpContext, StatusCodes.Status403Forbidden, "You are not allowed to do this.", "forbidden");
    }

    private static Task Deny(HttpContext http, int status, string message, string code)
    {
        http.Response.StatusCode = status;
        return http.Response.WriteAsJsonAsync(ApiResponse.Fail(message, new Dictionary<string, string[]> { ["code"] = [code] }, http.TraceIdentifier));
    }
}
