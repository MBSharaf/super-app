using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using SuperApp.Application.Features.Pitches;
using SuperApp.Application.Features.Reviews;

namespace SuperApp.Api.Controllers;

/// <summary>Public pitches (API §3.1): search and the pitch page. Live pitches only.</summary>
[Route("api/v1/pitches")]
public sealed class PitchesController(IPitchesService pitches, IAvailabilityService availability, IReviewsService reviews) : ApiControllerBase
{
    /// <summary>?q &amp;areaId &amp;governorateId &amp;format=5|7|11 &amp;minPrice &amp;maxPrice &amp;facilities=lights,parking &amp;lat &amp;lng &amp;sort=rating|price|-price|distance|name &amp;page &amp;pageSize</summary>
    [HttpGet]
    public async Task<ActionResult<object>> Search([FromQuery] PitchSearchQuery query, CancellationToken ct) => OkPage(await pitches.SearchAsync(query, ct));

    [HttpGet("{slug}")]
    public async Task<ActionResult<object>> Get(string slug, CancellationToken ct) => Ok(await pitches.GetAsync(slug, ct));

    /// <summary>?from=yyyy-MM-dd &amp;days=1..14 — the hour grid (cached 30 seconds).</summary>
    [HttpGet("{slug}/availability")]
    [OutputCache(PolicyName = AvailabilityCache)]
    public async Task<ActionResult<object>> Availability(string slug, [FromQuery] AvailabilityQuery query, CancellationToken ct) =>
        Ok(await availability.GetAsync(slug, query, ct));

    /// <summary>?page &amp;pageSize — visible reviews, newest first, with the venue's replies.</summary>
    [HttpGet("{slug}/reviews")]
    public async Task<ActionResult<object>> Reviews(string slug, [FromQuery] ReviewsQuery query, CancellationToken ct) => OkPage(await reviews.ForPitchAsync(slug, query, ct));

    public const string AvailabilityCache = "availability";
}
