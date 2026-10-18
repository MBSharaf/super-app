using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Hubs;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Features.Communities;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Communities (API §6.3) — forum only, no private messages. Public communities can be read without an account;
/// sellers-only ones need the role (403 audience). Live updates: hub <c>/hubs/community</c>.
/// </summary>
[Route("api/v1")]
[SlowModeFilter]
public sealed class CommunitiesController(ICommunitiesService communities, IPostsService posts) : ApiControllerBase
{
    private const long MaxUpload = 6 * 1024 * 1024;

    [HttpGet("communities")]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await communities.ListAsync(ct));

    [HttpGet("communities/feed")]
    [Authorize]
    public async Task<ActionResult<object>> Feed([FromQuery] PostsQuery query, CancellationToken ct) => OkPage(await communities.FeedAsync(query, ct));

    [HttpGet("communities/{slug}")]
    public async Task<ActionResult<object>> Get(string slug, CancellationToken ct) => Ok(await communities.GetAsync(slug, ct));

    [HttpPost("communities/{slug}/join")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Join(string slug, CancellationToken ct) => Ok(await communities.JoinAsync(slug, ct), "Welcome to the community.");

    [HttpDelete("communities/{slug}/join")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Leave(string slug, CancellationToken ct)
    {
        await communities.LeaveAsync(slug, ct);
        return Ok<object?>(null, "You left the community.");
    }

    /// <summary>?filter=latest|top|pinned|polls</summary>
    [HttpGet("communities/{slug}/posts")]
    public async Task<ActionResult<object>> Posts(string slug, [FromQuery] PostsQuery query, CancellationToken ct) => OkPage(await communities.PostsAsync(slug, query, ct));

    /// <summary>403 muted / not_member · 429 slow_mode · 422 outside_link / phone_number / banned_words · 423 community switched off.</summary>
    [HttpPost("communities/{slug}/posts")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> CreatePost(string slug, CreatePostRequest request, CancellationToken ct) => Created(await posts.CreateAsync(slug, request, ct), "Posted.");

    [HttpGet("posts/{id:long}")]
    public async Task<ActionResult<object>> Post(long id, CancellationToken ct) => Ok(await communities.PostAsync(id, ct));

    [HttpPut("posts/{id:long}")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> EditPost(long id, EditPostRequest request, CancellationToken ct) => Ok(await posts.EditAsync(id, request, ct), "Saved.");

    [HttpDelete("posts/{id:long}")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> DeletePost(long id, CancellationToken ct)
    {
        await posts.DeleteAsync(id, ct);
        return Ok<object?>(null, "Deleted.");
    }

    /// <summary>Multipart field <c>image</c> on my own post.</summary>
    [HttpPost("posts/{id:long}/image")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> Image(long id, [FromForm(Name = "image")] IFormFile image, CancellationToken ct) =>
        Ok(await posts.ImageAsync(id, new UploadedFile(image.OpenReadStream(), image.FileName, image.ContentType, image.Length), ct), "Photo added.");

    [HttpGet("posts/{id:long}/comments")]
    public async Task<ActionResult<object>> Comments(long id, CancellationToken ct) => Ok(await communities.CommentsAsync(id, ct));

    [HttpPost("posts/{id:long}/comments")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Comment(long id, CommentRequest request, CancellationToken ct) => Created(await posts.CommentAsync(id, null, request, ct), "Comment added.");

    [HttpPost("posts/{postId:long}/comments/{id:long}/replies")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reply(long postId, long id, CommentRequest request, CancellationToken ct) => Created(await posts.CommentAsync(postId, id, request, ct), "Reply added.");

    [HttpPut("posts/{id:long}/reaction")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> React(long id, ReactionRequest request, CancellationToken ct) => Ok(await posts.ReactAsync(id, request, ct));

    [HttpDelete("posts/{id:long}/reaction")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Unreact(long id, CancellationToken ct) => Ok(await posts.ReactAsync(id, null, ct));

    /// <summary>409 already_voted.</summary>
    [HttpPost("posts/{id:long}/vote")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Vote(long id, VoteRequest request, CancellationToken ct) => Ok(await posts.VoteAsync(id, request, ct), "Vote counted.");

    [HttpPost("posts/{id:long}/report")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ReportPost(long id, ReportRequest request, CancellationToken ct)
    {
        await posts.ReportAsync(id, null, request, ct);
        return Ok<object?>(null, "Thanks — the Super App team will look at it.");
    }

    [HttpPost("comments/{id:long}/report")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ReportComment(long id, ReportRequest request, CancellationToken ct)
    {
        await posts.ReportAsync(null, id, request, ct);
        return Ok<object?>(null, "Thanks — the Super App team will look at it.");
    }

    /// <summary>Rich card for a Super App link; outside links → 422 outside_link.</summary>
    [HttpGet("link-preview")]
    [Authorize]
    public async Task<ActionResult<object>> Preview([FromQuery] string url, CancellationToken ct) => Ok(await posts.PreviewAsync(url, ct));
}
