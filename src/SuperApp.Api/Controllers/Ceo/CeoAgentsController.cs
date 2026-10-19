using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Agents;

namespace SuperApp.Api.Controllers.Ceo;

/// <summary>The AI panel's agents (API §8 "AI agents"). CEO only, with a 2FA session; every change is audited.</summary>
[Route("api/v1/ceo/agents")]
[Authorize(Policy = Policies.Ceo)]
[EnableRateLimiting(RateLimitPolicies.Write)]
public sealed class CeoAgentsController(IAgentsService agents) : ApiControllerBase
{
    [HttpGet]
    [DisableRateLimiting]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await agents.ListAsync(ct));

    /// <summary>The card plus keys (never the key itself), tasks and the latest 20 runs.</summary>
    [HttpGet("{id:long}")]
    [DisableRateLimiting]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await agents.GetAsync(id, ct));

    /// <summary>Makes the agent and its service account; it starts Paused with no key.</summary>
    [HttpPost]
    public async Task<ActionResult<object>> Create(CreateAgentRequest request, CancellationToken ct) =>
        Created(await agents.CreateAsync(request, ct), "Agent created. Give it a key, then resume it.");

    [HttpPut("{id:long}")]
    public async Task<ActionResult<object>> Update(long id, UpdateAgentRequest request, CancellationToken ct) => Ok(await agents.UpdateAsync(id, request, ct), "Saved.");

    [HttpPut("{id:long}/permissions")]
    public async Task<ActionResult<object>> Permissions(long id, AgentPermissionsRequest request, CancellationToken ct) =>
        Ok(await agents.SetPermissionsAsync(id, request, ct), "Permissions saved.");

    [HttpPut("{id:long}/tasks")]
    public async Task<ActionResult<object>> Tasks(long id, AgentTasksRequest request, CancellationToken ct) => Ok(await agents.SetTasksAsync(id, request, ct), "Tasks saved.");

    [HttpPost("{id:long}/pause")]
    public async Task<ActionResult<object>> Pause(long id, ReasonRequest request, CancellationToken ct) => Ok(await agents.PauseAsync(id, request, ct), "Agent paused.");

    /// <summary>409 <c>agents_stopped</c> while the emergency stop is on.</summary>
    [HttpPost("{id:long}/resume")]
    public async Task<ActionResult<object>> Resume(long id, CancellationToken ct) => Ok(await agents.ResumeAsync(id, ct), "Agent running.");

    /// <summary>A new key — shown once in this answer; only its hash is kept.</summary>
    [HttpPost("{id:long}/keys")]
    public async Task<ActionResult<object>> CreateKey(long id, CreateApiKeyRequest request, CancellationToken ct) =>
        Created(await agents.CreateKeyAsync(id, request, ct), "Copy the key now — it is not shown again.");

    [HttpPost("{id:long}/keys/{keyId:long}/revoke")]
    public async Task<ActionResult<object>> RevokeKey(long id, long keyId, CancellationToken ct) => Ok(await agents.RevokeKeyAsync(id, keyId, ct), "Key revoked.");

    /// <summary>Stops every agent at once (switch kill.agents off).</summary>
    [HttpPost("emergency-stop")]
    public async Task<ActionResult<object>> EmergencyStop(ReasonRequest request, CancellationToken ct) => Ok(await agents.EmergencyStopAsync(request, ct), "Every agent is stopped.");

    [HttpPost("emergency-release")]
    public async Task<ActionResult<object>> EmergencyRelease(CancellationToken ct) => Ok(await agents.EmergencyReleaseAsync(ct), "Agents released.");

    /// <summary>Keys revoked, history kept.</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult<object>> Delete(long id, CancellationToken ct)
    {
        await agents.DeleteAsync(id, ct);
        return Ok(new { deleted = true }, "Agent deleted.");
    }
}
