namespace SuperApp.Application.Abstractions.Identity;

/// <summary>The sign-in providers the site offers besides email and password.</summary>
public static class ExternalProviders
{
    public const string Google = "Google";
    public const string Facebook = "Facebook";

    public static readonly IReadOnlyList<string> All = [Google, Facebook];
}

/// <summary>What a provider tells us about the person, after the server checked the token with the provider itself.</summary>
public sealed record ExternalProfile(string Provider, string ProviderKey, string Email, bool EmailVerified, string FullName);

/// <summary>
/// Checks a token the browser got from Google or Facebook. Returns null when the token is not valid, expired,
/// or was issued for another app — the browser is never trusted to say who the person is.
/// </summary>
public interface IExternalLoginVerifier
{
    string Provider { get; }

    Task<ExternalProfile?> VerifyAsync(string token, CancellationToken ct);
}
