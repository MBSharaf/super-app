using SuperApp.Application.Features.Ceo.Dtos;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Abstractions.Identity;

/// <summary>Reads and changes team (admin) accounts: roles, direct permissions, money limits, status, invitations.</summary>
public interface IStaffStore
{
    Task<IReadOnlyList<AdminResponse>> ListAdminsAsync(CancellationToken ct);

    Task<AdminResponse?> GetAdminAsync(Guid userId, CancellationToken ct);

    Task<bool> EmailExistsAsync(string email, CancellationToken ct);

    Task<bool> IsCeoAsync(Guid userId, CancellationToken ct);

    /// <summary>Creates the account without a password; the person sets it from the invitation.</summary>
    Task<Guid> CreateAdminAsync(NewAdmin admin, CancellationToken ct);

    Task UpdateAdminAsync(Guid userId, string fullName, IReadOnlyList<string> roles, CancellationToken ct);

    Task SetDirectPermissionsAsync(Guid userId, IReadOnlyList<string> codes, DateTime? expiresAt, Guid grantedBy, CancellationToken ct);

    Task SetLimitsAsync(Guid userId, StaffLimitsDto limits, CancellationToken ct);

    /// <summary>Changes the status; suspension ends every open session of the account.</summary>
    Task SetStatusAsync(Guid userId, UserStatus status, string? reason, CancellationToken ct);

    /// <summary>A 7-day link token for the invitation email.</summary>
    Task<string> CreateInviteTokenAsync(Guid userId);

    /// <summary>Checks the invitation token, sets the first password and confirms the email.</summary>
    Task AcceptInviteAsync(Guid userId, string token, string password);

    Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken ct);

    /// <summary>Names of the roles the CEO can give to admins (kind AdminRole).</summary>
    Task<IReadOnlySet<string>> AdminRoleNamesAsync(CancellationToken ct);

    Task<RoleResponse?> RoleAsync(Guid id, CancellationToken ct);

    Task<bool> RoleNameTakenAsync(string name, Guid? except, CancellationToken ct);

    /// <summary>Creates (id null) or updates an admin role and replaces its permissions; returns its id.</summary>
    Task<Guid> SaveRoleAsync(Guid? id, string name, string? description, IReadOnlyList<string> permissions, CancellationToken ct);

    Task DeleteRoleAsync(Guid id, CancellationToken ct);
}
