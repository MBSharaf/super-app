using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Tournaments;
using SuperApp.Domain.Enums;

namespace SuperApp.Api.Controllers;

/// <summary>Tournaments for players (API §3.9): list, detail, register a team (pays the entry fee), withdraw.</summary>
[Route("api/v1/tournaments")]
public sealed class TournamentsController(TournamentsService tournaments) : ApiControllerBase
{
    /// <summary>?status=open|full|running|finished</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<object>> List([FromQuery] string? status, CancellationToken ct) => Ok(await tournaments.ListAsync(status, ct));

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await tournaments.GetAsync(id, ct));

    /// <summary>422 registration_closed / full · 409 already_registered / team_name_taken. With Paymob the answer has paymentUrl.</summary>
    [HttpPost("{id:long}/teams")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Register(long id, RegisterTeamRequest request, CancellationToken ct) =>
        Created(await tournaments.RegisterAsync(id, request, ct), "Team registered.");

    [HttpPost("{id:long}/teams/{teamId:long}/withdraw")]
    [Authorize]
    public async Task<ActionResult<object>> Withdraw(long id, long teamId, CancellationToken ct) => Ok(await tournaments.WithdrawAsync(id, teamId, ct), "Team withdrawn.");
}

/// <summary>The venue's tournaments: create (hours reserved), teams, draw, results, cancel (refunds).</summary>
[Route("api/v1/partner/tournaments")]
[Authorize]
public sealed class PartnerTournamentsController(PartnerTournamentsService tournaments) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await tournaments.MineAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await tournaments.GetAsync(id, ct));

    /// <summary>409 when a match hour is taken · 422 outside opening hours / too_soon.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(CreateTournamentRequest request, CancellationToken ct) => Created(await tournaments.CreateAsync(request, ct), "Tournament created.");

    [HttpPost("{id:long}/teams")]
    public async Task<ActionResult<object>> AddTeam(long id, VenueTeamRequest request, CancellationToken ct) => Ok(await tournaments.AddTeamAsync(id, request, ct));

    [HttpDelete("{id:long}/teams/{teamId:long}")]
    public async Task<ActionResult<object>> RemoveTeam(long id, long teamId, CancellationToken ct) => Ok(await tournaments.RemoveTeamAsync(id, teamId, ct));

    [HttpPost("{id:long}/draw")]
    public async Task<ActionResult<object>> Draw(long id, CancellationToken ct) => Ok(await tournaments.DrawAsync(id, ct), "Fixtures published.");

    [HttpPut("{id:long}/matches/{matchId:long}")]
    public async Task<ActionResult<object>> Result(long id, long matchId, MatchResultRequest request, CancellationToken ct) => Ok(await tournaments.ResultAsync(id, matchId, request, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<object>> Cancel(long id, CancellationToken ct) => Ok(await tournaments.CancelAsync(id, ct), "Tournament cancelled.");
}

/// <summary>Admins oversee every tournament and can cancel one (refunds every paid team).</summary>
[Route("api/v1/admin/tournaments")]
[Authorize]
[RequirePermission("tournaments.manage")]
public sealed class AdminTournamentsController(ITournamentStore store, TournamentViews views, PartnerTournamentsService tournaments, ICurrentUser currentUser) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await views.ViewAsync(await store.AllAsync(ct), organiser: true, ct));

    [HttpPost("{id:long}/cancel")]
    public async Task<ActionResult<object>> Cancel(long id, CancellationToken ct)
    {
        var t = await store.FindAsync(id, ct) ?? throw new NotFoundException("Tournament not found.");
        await tournaments.CancelAsync(t, currentUser.UserId ?? throw new UnauthorizedException(), AuditSystem.Admin, ct);
        return Ok(await views.ViewAsync(t, organiser: true, ct), "Tournament cancelled.");
    }
}
