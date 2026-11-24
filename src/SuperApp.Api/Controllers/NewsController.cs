using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Extensions;
using SuperApp.Api.Security;
using SuperApp.Application.Features.News;

namespace SuperApp.Api.Controllers;

/// <summary>News written by the team: newest first, or one club's stories (?club=id).</summary>
[Route("api/v1/news")]
public sealed class NewsController(INewsService news) : ApiControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> List([FromQuery] long? club, [FromQuery] int page = 1, CancellationToken ct = default) =>
        Ok(await news.ListAsync(club, page, ct));

    [HttpGet("{id:long}")]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await news.GetAsync(id, ct));
}

/// <summary>The news editor (permission content.news + 2FA): write, edit, hide. Publishing a story tells its audience.</summary>
[Route("api/v1/admin/news")]
[RequirePermission("content.news")]
public sealed class AdminNewsController(INewsService news) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] int page = 1, CancellationToken ct = default) => Ok(await news.AdminListAsync(page, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await news.AdminGetAsync(id, ct));

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(NewsPostRequest request, CancellationToken ct) =>
        Created(await news.SaveAsync(null, request, ct), "Story published.");

    [HttpPut("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Edit(long id, NewsPostRequest request, CancellationToken ct) =>
        Ok(await news.SaveAsync(id, request, ct), "Story saved.");

    [HttpDelete("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Hide(long id, CancellationToken ct)
    {
        await news.DeleteAsync(id, ct);
        return Ok<object?>(null, "Story hidden.");
    }
}
