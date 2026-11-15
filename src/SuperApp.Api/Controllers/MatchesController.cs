using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SuperApp.Api.Extensions;
using SuperApp.Application.Features.Content;

namespace SuperApp.Api.Controllers;

/// <summary>Real matches (API §6.2): fixtures and results of every followed competition, from the cache the sync job refreshes.</summary>
[Route("api/v1/matches")]
public sealed class MatchesController(IMatchesService matches) : ApiControllerBase
{
    /// <summary>?date=yyyy-MM-dd (Cairo day, default today) &amp;days=1–14 &amp;competition=epl|egy|ucl…</summary>
    [HttpGet]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> List([FromQuery] DateOnly? date, [FromQuery] string? competition, [FromQuery] int days = 1, CancellationToken ct = default) =>
        Ok(await matches.MatchesAsync(date, days, competition, ct));

    /// <summary>The competitions on the matches page, in order, with their colour and whether they have fantasy.</summary>
    [HttpGet("competitions")]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> Competitions(CancellationToken ct) => Ok(await matches.CompetitionsAsync(ct));

    /// <summary>The match with every player's line (minutes, goals, assists, cards, fantasy points).</summary>
    [HttpGet("{id:long}")]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await matches.MatchAsync(id, ct));
}
