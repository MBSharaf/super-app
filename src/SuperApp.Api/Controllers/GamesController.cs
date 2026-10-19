using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Games;

namespace SuperApp.Api.Controllers;

/// <summary>Find players (API §3.8): open games are public; posting, joining and deciding need an account.</summary>
[Route("api/v1")]
[Authorize]
public sealed class GamesController(IGamesService games) : ApiControllerBase
{
    /// <summary>?areaId &amp;date &amp;level</summary>
    [HttpGet("games")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Open([FromQuery] GamesQuery query, CancellationToken ct) => Ok(await games.OpenAsync(query, ct));

    [HttpGet("games/{code}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await games.GetAsync(code, ct));

    /// <summary>Post one of my upcoming bookings as a game. Not mine → 422 booking_not_mine; already posted → 409 game_exists.</summary>
    [HttpPost("games")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Post(PostGameRequest request, CancellationToken ct) => Created(await games.PostAsync(request, ct), "Game posted.");

    [HttpPut("games/{code}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Edit(string code, EditGameRequest request, CancellationToken ct) => Ok(await games.EditAsync(code, request, ct), "Saved.");

    /// <summary>Ask to join; a full game puts the request on its waitlist. Twice → 409 already_asked.</summary>
    [HttpPost("games/{code}/requests")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Join(string code, JoinGameRequest request, CancellationToken ct) => Created(await games.JoinAsync(code, request, ct), "Request sent.");

    /// <summary>action = accept | decline | remove (host only).</summary>
    [HttpPost("games/{code}/requests/{id:long}/{decision:regex(^(accept|decline|remove)$)}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Decide(string code, long id, string decision, CancellationToken ct) => Ok(await games.DecideAsync(code, id, decision, ct));

    [HttpDelete("games/{code}/requests/mine")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Withdraw(string code, CancellationToken ct) => Ok(await games.WithdrawAsync(code, ct), "You left the game.");

    /// <summary>Host rates the players after kick-off: [{ playerId, stars, showedUp }].</summary>
    [HttpPost("games/{code}/ratings")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Rate(string code, RatingsRequest request, CancellationToken ct)
    {
        await games.RateAsync(code, request, ct);
        return Ok<object?>(null, "Thanks — ratings saved.");
    }

    [HttpGet("players/{userId:guid}/record")]
    public async Task<ActionResult<object>> Record(Guid userId, CancellationToken ct) => Ok(await games.RecordAsync(userId, ct));
}
