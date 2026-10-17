namespace SuperApp.Application.Features.Auth.Dtos;

/// <summary>Answer to sign-in: either signed in, or the 2FA step is needed (admins and the CEO).</summary>
public sealed record SignInResponse(bool SignedIn, bool Requires2fa, MeResponse? Me);

/// <summary>GET /me — the signed-in person, their roles, permissions and the dashboards they can open.</summary>
public sealed record MeResponse(
    Guid Id,
    string FullName,
    string Email,
    bool EmailConfirmed,
    string? Phone,
    string Language,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Dashboards,
    bool TwoFactorEnabled,
    DateTime CreatedAt);

/// <summary>GET /auth/csrf — the anti-forgery token to send in the X-CSRF-TOKEN header.</summary>
public sealed record CsrfResponse(string Token, string HeaderName);
