using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Identity;

/// <summary>
/// Builds the signed-in identity: roles (from Identity), the person's name, every permission code they hold,
/// a "staff" flag for admins / the CEO, and the sign-in time used to end staff sessions after 8 hours.
/// </summary>
public sealed class AppClaimsPrincipalFactory(
    UserManager<User> users,
    RoleManager<Role> roles,
    IOptions<IdentityOptions> options,
    PermissionReader permissions,
    IClock clock) : UserClaimsPrincipalFactory<User, Role>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(User user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var roleNames = identity.FindAll(identity.RoleClaimType).Select(c => c.Value).ToList();
        var codes = await permissions.ForUserAsync(user.Id, roleNames, CancellationToken.None);

        identity.AddClaim(new Claim(AppClaims.FullName, user.FullName));
        identity.AddClaims(codes.Select(code => new Claim(AppClaims.Permission, code)));
        if (codes.Count > 0)
        {
            identity.AddClaim(new Claim(AppClaims.Staff, "1"));
        }
        identity.AddClaim(new Claim(AppClaims.SignedInAt, clock.UtcNow.ToString("O", CultureInfo.InvariantCulture)));
        return identity;
    }
}

/// <summary>Names of the claims the app adds to the session.</summary>
public static class AppClaims
{
    public const string FullName = "name";
    public const string Permission = "permission";
    public const string Staff = "staff";
    public const string SignedInAt = "signed_in_at";

    /// <summary>Set by Identity after the authenticator code was checked ("mfa").</summary>
    public const string AuthMethod = "amr";
}
