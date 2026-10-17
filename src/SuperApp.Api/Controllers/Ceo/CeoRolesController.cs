using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Ceo.Services;

namespace SuperApp.Api.Controllers.Ceo;

/// <summary>Roles and the permission catalogue for the CEO's permission matrix (read; editing roles comes with Phase 7).</summary>
[Route("api/v1/ceo")]
[Authorize(Policy = Policies.Ceo)]
public sealed class CeoRolesController(IAdminAccountsService admins) : ApiControllerBase
{
    [HttpGet("roles")]
    public async Task<ActionResult<object>> Roles(CancellationToken ct) => Ok(await admins.RolesAsync(ct));

    [HttpGet("permissions")]
    public ActionResult<object> Permissions() => Ok(admins.PermissionCatalogue());
}
