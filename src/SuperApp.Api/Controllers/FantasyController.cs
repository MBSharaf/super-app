using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Fantasy;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Fantasy (API §6.1) for the Egyptian (<c>EGY</c>) and English (<c>EPL</c>) leagues. Players, fixtures and the table
/// are public and come from the cache; the team endpoints need a signed-in account. Changes go to the gameweek open
/// for changes; with none left the answer is 423 <c>deadline_passed</c>.
/// </summary>
[Route("api/v1/fantasy")]
public sealed class FantasyController(IFantasyService fantasy, IFantasyTeamService teams) : ApiControllerBase
{
    [HttpGet("competitions")]
    public async Task<ActionResult<object>> Competitions(CancellationToken ct) => Ok(await fantasy.CompetitionsAsync(ct));

    /// <summary>Competition, clubs (kit colours, no logos), every player and the gameweeks — one call for the fantasy pages.</summary>
    [HttpGet("{comp}/bootstrap")]
    public async Task<ActionResult<object>> Bootstrap(string comp, CancellationToken ct) => Ok(await fantasy.BootstrapAsync(comp, ct));

    [HttpGet("{comp}/players")]
    public async Task<ActionResult<object>> Players(string comp, [FromQuery] PlayersQuery query, CancellationToken ct) =>
        OkPage(await fantasy.PlayersAsync(comp, query, ct));

    [HttpGet("{comp}/players/{id:long}")]
    public async Task<ActionResult<object>> Player(string comp, long id, CancellationToken ct) => Ok(await fantasy.PlayerAsync(comp, id, ct));

    [HttpGet("{comp}/fixtures")]
    public async Task<ActionResult<object>> Fixtures(string comp, [FromQuery] int? gameweek, CancellationToken ct) =>
        Ok(await fantasy.FixturesAsync(comp, gameweek, ct));

    /// <summary>The real league table from finished matches.</summary>
    [HttpGet("{comp}/table")]
    public async Task<ActionResult<object>> Table(string comp, CancellationToken ct) => Ok(await fantasy.TableAsync(comp, ct));

    /// <summary>My squad for the gameweek open for changes — 404 <c>no_team</c> → create one.</summary>
    [Authorize]
    [HttpGet("{comp}/team")]
    public async Task<ActionResult<object>> Team(string comp, CancellationToken ct) => Ok(await teams.GetAsync(comp, ct));

    /// <summary>422 squad_size / formation / max_per_club / over_budget; 409 team_exists.</summary>
    [Authorize]
    [HttpPost("{comp}/team")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(string comp, CreateTeamRequest request, CancellationToken ct) =>
        Created(await teams.CreateAsync(comp, request, ct), "Your team is ready.");

    [Authorize]
    [HttpPatch("{comp}/team")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Rename(string comp, RenameTeamRequest request, CancellationToken ct) => Ok(await teams.RenameAsync(comp, request, ct));

    [Authorize]
    [HttpPut("{comp}/team/lineup")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Lineup(string comp, LineupRequest request, CancellationToken ct) =>
        Ok(await teams.LineupAsync(comp, request, ct), "Lineup saved.");

    /// <summary>Free transfers first, then 4 points each (none with a wildcard). 422 over_budget / transfer_position.</summary>
    [Authorize]
    [HttpPost("{comp}/team/transfers")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Transfers(string comp, TransfersRequest request, CancellationToken ct) =>
        Ok(await teams.TransfersAsync(comp, request, ct), "Transfers made.");

    /// <summary>wildcard (one per half of the season), benchboost or triplecaptain — 409 chip_used / chip_this_week.</summary>
    [Authorize]
    [HttpPost("{comp}/team/chips/{chip}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Chip(string comp, string chip, CancellationToken ct) => Ok(await teams.ChipAsync(comp, chip, ct), "Chip played.");

    [Authorize]
    [HttpGet("{comp}/team/history")]
    public async Task<ActionResult<object>> History(string comp, CancellationToken ct) => Ok(await teams.HistoryAsync(comp, ct));
}

/// <summary>
/// Fantasy groups and the Fantasy Pass (API §6.1): my groups, official sponsor leagues (Fantasy Pass), private groups by
/// invite code, paid groups (hidden until payments are live) and the tables.
/// </summary>
[Route("api/v1/fantasy")]
[Authorize]
public sealed class FantasyGroupsController(IFantasyGroupsService groups, IFantasyPassService passes) : ApiControllerBase
{
    /// <summary>The grand league (my overall rank), my groups, official leagues, paid groups (empty until payments are live).</summary>
    [HttpGet("{comp}/groups")]
    public async Task<ActionResult<object>> Overview(string comp, CancellationToken ct) => Ok(await groups.OverviewAsync(comp, ct));

    /// <summary>A private group with an invite code — 422 no_team without a team in this league.</summary>
    [HttpPost("{comp}/groups")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(string comp, CreateGroupRequest request, CancellationToken ct) =>
        Created(await groups.CreateAsync(comp, request, ct), "Group created. Share the invite code.");

    /// <summary>404 unknown code · 409 already_member · 403 pass_required (official) · 422 payment_required (paid).</summary>
    [HttpPost("groups/join")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Join(JoinGroupRequest request, CancellationToken ct) => Ok(await groups.JoinAsync(request, ct), "You joined the group.");

    [HttpPost("groups/{id:long}/leave")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Leave(long id, CancellationToken ct)
    {
        await groups.LeaveAsync(id, ct);
        return Ok<object?>(null, "You left the group.");
    }

    /// <summary>Official groups are public; other groups only for their members (403 not_member).</summary>
    [HttpGet("groups/{id:long}/table")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Table(long id, CancellationToken ct) => Ok(await groups.TableAsync(id, ct));

    [HttpGet("pass")]
    public async Task<ActionResult<object>> Pass(CancellationToken ct) => Ok(await passes.StatusAsync(ct));

    /// <summary>{ plan: egy | epl | both } + header Idempotency-Key; a repeat within 10 minutes buys nothing more.</summary>
    [HttpPost("pass")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> BuyPass(BuyPassRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct) =>
        Ok(await passes.BuyAsync(request, idempotencyKey, ct), "Fantasy Pass active.");
}
