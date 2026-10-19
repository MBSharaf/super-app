using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.CeoSystem;

namespace SuperApp.Api.Controllers.Ceo;

/// <summary>The org chart and the integrations registry (API §8). CEO only, 2FA; every change is audited.</summary>
[Route("api/v1/ceo")]
[Authorize(Policy = Policies.Ceo)]
public sealed class CeoOrgController(IOrgService org, IIntegrationsService integrations) : ApiControllerBase
{
    /// <summary>Departments and positions with their holders (people and agents).</summary>
    [HttpGet("org")]
    public async Task<ActionResult<object>> Org(CancellationToken ct) => Ok(await org.ChartAsync(ct));

    [HttpPost("org/positions")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> CreatePosition(PositionRequest request, CancellationToken ct) => Created(await org.CreateAsync(request, ct), "Position added.");

    [HttpPut("org/positions/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> UpdatePosition(long id, PositionRequest request, CancellationToken ct) => Ok(await org.UpdateAsync(id, request, ct), "Saved.");

    /// <summary>409 <c>has_reports</c> while other positions report to it.</summary>
    [HttpDelete("org/positions/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> DeletePosition(long id, CancellationToken ct)
    {
        await org.DeleteAsync(id, ct);
        return Ok(new { deleted = true }, "Position removed.");
    }

    [HttpGet("integrations")]
    public async Task<ActionResult<object>> Integrations(CancellationToken ct) => Ok(await integrations.ListAsync(ct));

    /// <summary>One integration with 30 days of usage.</summary>
    [HttpGet("integrations/{key}")]
    public async Task<ActionResult<object>> Integration(string key, CancellationToken ct) => Ok(await integrations.GetAsync(key, ct));

    /// <summary>{ mode, isEnabled, isPrimary, config } — Test / Live need the secret in the server's .env (422 secret_missing).</summary>
    [HttpPut("integrations/{key}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> UpdateIntegration(string key, UpdateIntegrationRequest request, CancellationToken ct) =>
        Ok(await integrations.UpdateAsync(key, request, ct), "Saved.");

    [HttpPost("integrations/{key}/test")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> TestIntegration(string key, CancellationToken ct) => Ok(await integrations.TestAsync(key, ct));
}
