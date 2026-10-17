using Microsoft.AspNetCore.Mvc;
using SuperApp.Application.Features.Pitches;

namespace SuperApp.Api.Controllers;

/// <summary>Public pitches (API §3.1): search and the pitch page. Live pitches only.</summary>
[Route("api/v1/pitches")]
public sealed class PitchesController(IPitchesService pitches) : ApiControllerBase
{
    /// <summary>?q &amp;areaId &amp;governorateId &amp;format=5|7|11 &amp;minPrice &amp;maxPrice &amp;facilities=lights,parking &amp;lat &amp;lng &amp;sort=rating|price|-price|distance|name &amp;page &amp;pageSize</summary>
    [HttpGet]
    public async Task<ActionResult<object>> Search([FromQuery] PitchSearchQuery query, CancellationToken ct) => OkPage(await pitches.SearchAsync(query, ct));

    [HttpGet("{slug}")]
    public async Task<ActionResult<object>> Get(string slug, CancellationToken ct) => Ok(await pitches.GetAsync(slug, ct));
}
