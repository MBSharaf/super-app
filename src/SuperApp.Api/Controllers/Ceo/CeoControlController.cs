using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.CeoSystem;

namespace SuperApp.Api.Controllers.Ceo;

/// <summary>Kill switches, the API catalogue and the AI panel summary (API §8). CEO only, 2FA; every change is audited.</summary>
[Route("api/v1/ceo")]
[Authorize(Policy = Policies.Ceo)]
public sealed class CeoControlController(ICeoControlService control) : ApiControllerBase
{
    [HttpGet("switches")]
    public async Task<ActionResult<object>> Switches(CancellationToken ct) => Ok(await control.SwitchesAsync(ct));

    /// <summary>{ enabled, reason } — off needs a reason; the area answers 423 at once.</summary>
    [HttpPut("switches/{key}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SetSwitch(string key, SetSwitchRequest request, CancellationToken ct) =>
        Ok(await control.SetSwitchAsync(key, request, ct), request.Enabled ? "Switched on." : "Switched off.");

    /// <summary>Every endpoint group: purpose, permission, callers, switch, and agent calls in 24 hours.</summary>
    [HttpGet("api-catalogue")]
    public async Task<ActionResult<object>> Catalogue(CancellationToken ct) => Ok(await control.CatalogueAsync(ct));

    [HttpPut("api-catalogue/{group}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SetGroup(string group, SetApiGroupRequest request, CancellationToken ct) =>
        Ok(await control.SetApiGroupAsync(group, request, ct), request.Enabled ? "Group on." : "Group off.");

    /// <summary>AI panel: agents by status, runs, cost this month, errors, cost per agent.</summary>
    [HttpGet("ai")]
    public async Task<ActionResult<object>> Ai(CancellationToken ct) => Ok(await control.AiPanelAsync(ct));
}
