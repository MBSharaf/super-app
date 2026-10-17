using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Identity;

/// <summary>
/// Session rules (PRD FR-AUTH-02): the security stamp is re-checked every minute, so suspension and "sign out
/// everywhere" take effect quickly; staff sessions end 8 hours after sign-in; reset links last 30 minutes and
/// email-confirmation links 2 days.
/// </summary>
public static class SessionRules
{
    public static readonly TimeSpan StaffSession = TimeSpan.FromHours(8);
    public const string EmailConfirmationProvider = "EmailConfirmation";

    public static IServiceCollection AddSessionRules(this IServiceCollection services)
    {
        services.Configure<SecurityStampValidatorOptions>(o =>
        {
            o.ValidationInterval = TimeSpan.FromMinutes(1);
            o.OnRefreshingPrincipal = KeepSignInFacts;
        });
        services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromMinutes(30));
        services.Configure<EmailConfirmationTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromDays(2));
        services.Configure<InviteTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromDays(7));
        services.Configure<IdentityOptions>(o => o.Tokens.EmailConfirmationTokenProvider = EmailConfirmationProvider);
        return services;
    }

    /// <summary>Checks the stamp, then ends a staff session that is older than 8 hours.</summary>
    public static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        var principal = context.Principal;
        if (principal?.HasClaim(AppClaims.Staff, "1") == true && SignedInAt(principal) is { } at && DateTime.UtcNow - at > StaffSession)
        {
            context.RejectPrincipal();
        }
    }

    /// <summary>A refreshed session keeps its original sign-in time and its 2FA mark.</summary>
    private static Task KeepSignInFacts(SecurityStampRefreshingPrincipalContext context)
    {
        if (context.NewPrincipal?.Identity is not ClaimsIdentity fresh || context.CurrentPrincipal is null)
        {
            return Task.CompletedTask;
        }
        foreach (var type in new[] { AppClaims.SignedInAt, AppClaims.AuthMethod })
        {
            fresh.FindAll(type).ToList().ForEach(c => fresh.RemoveClaim(c));
            fresh.AddClaims(context.CurrentPrincipal.FindAll(type).Select(c => new Claim(type, c.Value)));
        }
        return Task.CompletedTask;
    }

    private static DateTime? SignedInAt(ClaimsPrincipal principal) =>
        DateTime.TryParse(principal.FindFirstValue(AppClaims.SignedInAt), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at) ? at : null;
}

/// <summary>Email confirmation links get their own provider so they can last longer than reset links.</summary>
public sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public EmailConfirmationTokenProviderOptions() => Name = SessionRules.EmailConfirmationProvider;
}

public sealed class EmailConfirmationTokenProvider(
    Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtection,
    Microsoft.Extensions.Options.IOptions<EmailConfirmationTokenProviderOptions> options,
    Microsoft.Extensions.Logging.ILogger<DataProtectorTokenProvider<User>> logger)
    : DataProtectorTokenProvider<User>(dataProtection, options, logger);

/// <summary>Admin invitations last 7 days.</summary>
public sealed class InviteTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public InviteTokenProviderOptions() => Name = StaffStore.InviteProvider;
}

public sealed class InviteTokenProvider(
    Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtection,
    Microsoft.Extensions.Options.IOptions<InviteTokenProviderOptions> options,
    Microsoft.Extensions.Logging.ILogger<DataProtectorTokenProvider<User>> logger)
    : DataProtectorTokenProvider<User>(dataProtection, options, logger);
