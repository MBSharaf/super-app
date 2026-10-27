using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Ceo.Dtos;
using SuperApp.Application.Features.Ceo.Services;

namespace SuperApp.Api.Controllers.Ceo;

/// <summary>Roles and the permission catalogue for the CEO's permission matrix; the CEO adds and edits admin roles.</summary>
[Route("api/v1/ceo")]
[Authorize(Policy = Policies.Ceo)]
public sealed class CeoRolesController(IAdminAccountsService admins) : ApiControllerBase
{
    [HttpGet("roles")]
    public async Task<ActionResult<object>> Roles(CancellationToken ct) => Ok(await admins.RolesAsync(ct));

    [HttpPost("roles")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> CreateRole(RoleRequest request, CancellationToken ct) =>
        Created(await admins.CreateRoleAsync(request, ct), "Role added.");

    /// <summary>Name (not for seeded roles), description and the role's permissions.</summary>
    [HttpPut("roles/{id:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> UpdateRole(Guid id, RoleRequest request, CancellationToken ct) =>
        Ok(await admins.UpdateRoleAsync(id, request, ct), "Role saved. People with it get the change within a minute.");

    [HttpDelete("roles/{id:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> DeleteRole(Guid id, CancellationToken ct)
    {
        await admins.DeleteRoleAsync(id, ct);
        return Ok(new { deleted = true }, "Role deleted.");
    }

    [HttpGet("permissions")]
    public ActionResult<object> Permissions() => Ok(admins.PermissionCatalogue());
}
