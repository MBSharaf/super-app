using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Agents;

namespace SuperApp.Api.Controllers.Agent;

/// <summary>
/// The agent API (API §9.1) — called with <c>X-Api-Key</c> only. Agents also call any admin endpoint they hold the
/// permission for; these endpoints add identity, tasks and run reporting (cost counts against the monthly budget).
/// </summary>
[Route("api/v1/agent")]
[Authorize(Policy = AgentKeys.Policy)]
public sealed class AgentController(IAgentSelfService self) : ApiControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me(CancellationToken ct) => Ok(await self.MeAsync(ct));

    [HttpGet("tasks")]
    public async Task<ActionResult<object>> Tasks(CancellationToken ct) => Ok(await self.TasksAsync(ct));

    [HttpPost("runs")]
    public async Task<ActionResult<object>> StartRun(StartRunRequest request, CancellationToken ct) => Created(await self.StartRunAsync(request, ct), "Run started.");

    [HttpPatch("runs/{id:long}")]
    public async Task<ActionResult<object>> FinishRun(long id, FinishRunRequest request, CancellationToken ct) => Ok(await self.FinishRunAsync(id, request, ct), "Run saved.");
}
