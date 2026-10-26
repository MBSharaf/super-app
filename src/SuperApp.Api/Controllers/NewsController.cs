using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Extensions;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Features.Content;

namespace SuperApp.Api.Controllers;

/// <summary>Real matches (API §6.2): fixtures, live scores and results of the Egyptian and English leagues, from the cache the sync job refreshes.</summary>
[Route("api/v1/matches")]
public sealed class MatchesController(INewsService news) : ApiControllerBase
{
    /// <summary>?date=yyyy-MM-dd (Cairo day, default today) &amp;competition=EGY|EPL.</summary>
    [HttpGet]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> List([FromQuery] DateOnly? date, [FromQuery] string? competition, CancellationToken ct) =>
        Ok(await news.MatchesAsync(date, competition, ct));

    /// <summary>The match with every player's line (minutes, goals, assists, cards, fantasy points).</summary>
    [HttpGet("{id:long}")]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await news.MatchAsync(id, ct));
}

/// <summary>Super App news posts (API §6.2, FR-NEWS-01). Hidden and scheduled posts are left out.</summary>
[Route("api/v1/news")]
public sealed class NewsController(INewsService news) : ApiControllerBase
{
    [HttpGet]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> List([FromQuery] NewsQuery query, CancellationToken ct) => OkPage(await news.NewsAsync(query, false, ct));

    [HttpGet("{id:long}")]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await news.PostAsync(id, false, ct));
}

/// <summary>News posts for admins (permission content.news + 2FA): write, schedule, hide, photo, delete.</summary>
[Route("api/v1/admin/news")]
[RequirePermission("content.news")]
public sealed class AdminNewsController(INewsService news) : ApiControllerBase
{
    private const long MaxUpload = 6 * 1024 * 1024;

    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] NewsQuery query, CancellationToken ct) => OkPage(await news.NewsAsync(query, true, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<object>> Get(long id, CancellationToken ct) => Ok(await news.PostAsync(id, true, ct));

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(NewsRequest request, CancellationToken ct) => Created(await news.SaveAsync(null, request, ct), "Posted.");

    [HttpPut("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Edit(long id, NewsRequest request, CancellationToken ct) => Ok(await news.SaveAsync(id, request, ct), "Saved.");

    /// <summary>Multipart field <c>image</c> (JPEG / PNG / WEBP, re-encoded).</summary>
    [HttpPost("{id:long}/image")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> Image(long id, [FromForm(Name = "image")] IFormFile image, CancellationToken ct) =>
        Ok(await news.ImageAsync(id, new UploadedFile(image.OpenReadStream(), image.FileName, image.ContentType, image.Length), ct), "Photo saved.");

    [HttpDelete("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Delete(long id, CancellationToken ct)
    {
        await news.DeleteAsync(id, ct);
        return Ok<object?>(null, "Deleted.");
    }
}
