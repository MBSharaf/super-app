using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>Community reports (permission community.moderate + 2FA): remove, keep, or remove and mute the author.</summary>
[Route("api/v1/admin/community/reports")]
[RequirePermission("community.moderate")]
public sealed class AdminCommunityReportsController(ICommunityAdminService admin) : ApiControllerBase
{
    /// <summary>?status=Open (default) | Removed | Kept | RemovedAndMuted</summary>
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        OkPage(await admin.ReportsAsync(status, page, pageSize, ct));

    [HttpPost("{id:long}/remove")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Remove(long id, CancellationToken ct) => await Decide(id, ContentReportStatus.Removed, null, "Removed.", ct);

    [HttpPost("{id:long}/keep")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Keep(long id, CancellationToken ct) => await Decide(id, ContentReportStatus.Kept, null, "Kept.", ct);

    [HttpPost("{id:long}/mute-author")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Mute(long id, MuteRequest request, CancellationToken ct) => await Decide(id, ContentReportStatus.RemovedAndMuted, request, "Removed and the author muted.", ct);

    private async Task<ActionResult<object>> Decide(long id, ContentReportStatus decision, MuteRequest? mute, string message, CancellationToken ct)
    {
        await admin.DecideAsync(id, decision, mute, ct);
        return Ok<object?>(null, message);
    }
}

/// <summary>Communities, banned words and mutes (permission community.manage + 2FA).</summary>
[Route("api/v1/admin")]
[RequirePermission("community.manage")]
public sealed class AdminCommunitiesController(ICommunityAdminService admin) : ApiControllerBase
{
    [HttpGet("communities")]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await admin.CommunitiesAsync(ct));

    [HttpPost("communities")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(CommunityRequest request, CancellationToken ct) => Created(await admin.SaveCommunityAsync(null, request, ct), "Community created.");

    [HttpPut("communities/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Edit(long id, CommunityRequest request, CancellationToken ct) => Ok(await admin.SaveCommunityAsync(id, request, ct), "Community saved.");

    [HttpPost("communities/{id:long}/archive")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Archive(long id, [FromQuery] bool archived = true, CancellationToken ct = default)
    {
        await admin.ArchiveAsync(id, archived, ct);
        return Ok<object?>(null, archived ? "Archived." : "Restored.");
    }

    /// <summary>Pin, announcement, close comments, hide.</summary>
    [HttpPut("posts/{id:long}/flags")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Flags(long id, PostFlagsRequest request, CancellationToken ct) => Ok(await admin.FlagsAsync(id, request, ct), "Saved.");

    [HttpGet("banned-words")]
    public async Task<ActionResult<object>> Words(CancellationToken ct) => Ok(await admin.BannedWordsAsync(ct));

    [HttpPost("banned-words")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddWord(BannedWordRequest request, CancellationToken ct) => Created(await admin.AddBannedWordAsync(request, ct), "Word added.");

    [HttpDelete("banned-words/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RemoveWord(long id, CancellationToken ct)
    {
        await admin.RemoveBannedWordAsync(id, ct);
        return Ok<object?>(null, "Word removed.");
    }

    [HttpGet("mutes")]
    public async Task<ActionResult<object>> Mutes(CancellationToken ct) => Ok(await admin.MutesAsync(ct));

    [HttpDelete("mutes/{communityId:long}/{userId:guid}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Unmute(long communityId, Guid userId, CancellationToken ct)
    {
        await admin.UnmuteAsync(communityId, userId, ct);
        return Ok<object?>(null, "Unmuted.");
    }
}

/// <summary>Posts and replies as "Super App Admin" (permission community.postAsAdmin + 2FA).</summary>
[Route("api/v1/admin")]
[RequirePermission("community.postAsAdmin")]
public sealed class AdminCommunityPostsController(ICommunityAdminService admin) : ApiControllerBase
{
    [HttpPost("communities/{slug}/posts")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Post(string slug, AdminPostRequest request, CancellationToken ct) => Created(await admin.PostAsync(slug, request, ct), "Posted as Super App Admin.");

    [HttpPost("posts/{id:long}/reply")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reply(long id, CommentRequest request, CancellationToken ct) => Created(await admin.ReplyAsync(id, request, ct), "Replied as Super App Admin.");
}
