using System.Security.Claims;
using SuperApp.Application.Abstractions.Services;

namespace SuperApp.Api.Services;

/// <summary>Reads the caller from the current HTTP request (cookie or API key claims).</summary>
public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public const string PermissionClaim = "permission";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid? UserId => Guid.TryParse(Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public bool HasPermission(string permission) => Principal?.HasClaim(PermissionClaim, permission) == true;

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
