namespace SuperApp.Application.Abstractions.Identity;

/// <summary>Data for a new customer account.</summary>
public sealed record NewAccount(string FullName, string Email, string Phone, string Language);

/// <summary>The few facts about an account the auth flows need.</summary>
public sealed record AccountSummary(Guid Id, string FullName, string Email, string Language, bool EmailConfirmed, bool IsStaff);

/// <summary>Result of checking a password.</summary>
public enum SignInOutcome
{
    Succeeded,
    RequiresTwoFactor,
    LockedOut,
    NotAllowed,
    Failed,
}

/// <summary>Everything the site needs to build menus for the signed-in person (<c>GET /me</c>).</summary>
public sealed record ProfileData(
    Guid Id,
    string FullName,
    string Email,
    bool EmailConfirmed,
    string? Phone,
    string Language,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    bool TwoFactorEnabled,
    DateTime CreatedAt);
