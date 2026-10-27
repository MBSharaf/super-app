namespace SuperApp.Application.Features.Ceo.Dtos;

/// <summary>Money an admin may move alone (EGP); above it the action becomes an approval request.</summary>
public sealed record StaffLimitsDto(decimal Refund, decimal Credit, decimal Payout, bool CanSecondApprove);

/// <summary>A permission given directly to one admin (on top of their roles), optionally until a date.</summary>
public sealed record DirectGrantDto(string Code, DateTime? ExpiresAt);

/// <summary>One admin account as the CEO sees it.</summary>
public sealed record AdminResponse(
    Guid Id,
    string FullName,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<DirectGrantDto> DirectPermissions,
    IReadOnlyList<string> AllPermissions,
    StaffLimitsDto Limits,
    string Status,
    string? SuspendedReason,
    bool TwoFactorEnabled,
    bool InvitationPending,
    DateTime? LastSeenAt,
    DateTime CreatedAt);

/// <summary>POST /ceo/admins — the new admin gets an invitation email to choose a password.</summary>
public sealed record CreateAdminRequest(string FullName, string Email, List<string> Roles, List<string>? Permissions, StaffLimitsDto? Limits, string Language = "ar");

/// <summary>PUT /ceo/admins/{id}.</summary>
public sealed record UpdateAdminRequest(string FullName, List<string> Roles);

/// <summary>PUT /ceo/admins/{id}/permissions — replaces the direct grants (for example "approvals.partners" for one admin).</summary>
public sealed record SetPermissionsRequest(List<string> Permissions, DateTime? ExpiresAt);

/// <summary>POST /ceo/admins/{id}/suspend.</summary>
public sealed record SuspendRequest(string Reason);

/// <summary>GET /ceo/roles — a role and its permission codes (the permission matrix). Holders = accounts that have it.</summary>
public sealed record RoleResponse(Guid Id, string Name, string? Description, string Kind, bool IsLocked, IReadOnlyList<string> Permissions, int Holders);

/// <summary>POST /ceo/roles and PUT /ceo/roles/{id} — an admin role; a locked (seeded) role keeps its name.</summary>
public sealed record RoleRequest(string Name, string? Description, List<string> Permissions);

/// <summary>GET /ceo/permissions — the catalogue with names in both languages.</summary>
public sealed record PermissionResponse(string Code, string Area, string NameEn, string NameAr, bool IsSensitive);

/// <summary>Data for a new admin account (no password yet).</summary>
public sealed record NewAdmin(string FullName, string Email, IReadOnlyList<string> Roles, string Language);
