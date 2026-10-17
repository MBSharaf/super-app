namespace SuperApp.Application.Abstractions.Identity;

/// <summary>
/// Accounts, passwords and one-time tokens (implemented with ASP.NET Identity in Infrastructure).
/// Methods throw the app's exceptions (409 email taken, 400 weak password…) so callers stay simple.
/// </summary>
public interface IIdentityService
{
    Task<Guid> CreateCustomerAsync(NewAccount account, string password, CancellationToken ct);

    /// <summary>Finds an account by email or Egyptian phone number; null when there is none.</summary>
    Task<AccountSummary?> FindByLoginAsync(string emailOrPhone, CancellationToken ct);

    Task<AccountSummary?> FindByIdAsync(Guid userId, CancellationToken ct);

    Task<string> CreateEmailConfirmationTokenAsync(Guid userId);

    Task ConfirmEmailAsync(Guid userId, string token);

    Task<string> CreatePasswordResetTokenAsync(Guid userId);

    Task ResetPasswordAsync(Guid userId, string token, string newPassword);

    Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

    Task<ProfileData> GetProfileAsync(Guid userId, CancellationToken ct);

    Task UpdateProfileAsync(Guid userId, string? fullName, string? phone, string? language, CancellationToken ct);

    /// <summary>Adds a role (e.g. PitchOwner after approval); the open session picks it up at its next refresh (within a minute).</summary>
    Task AddRoleAsync(Guid userId, string role);
}
