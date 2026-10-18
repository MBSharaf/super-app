using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Content;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>Home banners and the announcement bar (permission content.banners + 2FA).</summary>
[Route("api/v1/admin")]
[RequirePermission("content.banners")]
public sealed class AdminBannersController(IContentAdminService admin) : ApiControllerBase
{
    [HttpGet("banners")]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await admin.BannersAsync(ct));

    [HttpPost("banners")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(BannerRequest request, CancellationToken ct) => Created(await admin.SaveBannerAsync(null, request, ct), "Banner added.");

    [HttpPut("banners/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Edit(long id, BannerRequest request, CancellationToken ct) => Ok(await admin.SaveBannerAsync(id, request, ct), "Banner saved.");

    [HttpDelete("banners/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Delete(long id, CancellationToken ct)
    {
        await admin.DeleteBannerAsync(id, ct);
        return Ok<object?>(null, "Banner removed.");
    }

    [HttpPut("announcement")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Announcement(AnnouncementDto request, CancellationToken ct) => Ok(await admin.AnnouncementAsync(request, ct), "Announcement saved.");
}

/// <summary>Pop-up ads and their timing (permission content.popups + 2FA); the list shows views and clicks.</summary>
[Route("api/v1/admin/popups")]
[RequirePermission("content.popups")]
public sealed class AdminPopupsController(IContentAdminService admin) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List(CancellationToken ct) => Ok(await admin.PopupsAsync(ct));

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(PopupRequest request, CancellationToken ct) => Created(await admin.SavePopupAsync(null, request, ct), "Pop-up added.");

    [HttpPut("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Edit(long id, PopupRequest request, CancellationToken ct) => Ok(await admin.SavePopupAsync(id, request, ct), "Pop-up saved.");

    [HttpDelete("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Delete(long id, CancellationToken ct)
    {
        await admin.DeletePopupAsync(id, ct);
        return Ok<object?>(null, "Pop-up removed.");
    }

    [HttpPut("timing")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Timing(PopupTimingDto request, CancellationToken ct) => Ok(await admin.TimingAsync(request, ct), "Timing saved.");
}

/// <summary>Help center articles (permission content.help + 2FA).</summary>
[Route("api/v1/admin/help")]
[RequirePermission("content.help")]
public sealed class AdminHelpController(ILegalHelpService help) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> List([FromQuery] HelpQuery query, CancellationToken ct) => Ok(await help.ArticlesAsync(query, true, ct));

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Create(HelpArticleRequest request, CancellationToken ct) => Created(await help.SaveArticleAsync(null, request, ct), "Article added.");

    [HttpPut("{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Edit(long id, HelpArticleRequest request, CancellationToken ct) => Ok(await help.SaveArticleAsync(id, request, ct), "Article saved.");
}

/// <summary>Legal versions (permission content.legal + 2FA): draft a version, publish it; the CEO marks the lawyer's review.</summary>
[Route("api/v1/admin/legal/{key}/versions")]
[RequirePermission("content.legal")]
public sealed class AdminLegalController(ILegalHelpService legal) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<object>> Versions(string key, CancellationToken ct) => Ok(await legal.VersionsAsync(key, ct));

    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Draft(string key, LegalVersionRequest request, CancellationToken ct) => Created(await legal.NewVersionAsync(key, request, ct), "Draft saved.");

    /// <summary>409 already_published.</summary>
    [HttpPost("{version:int}/publish")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Publish(string key, int version, CancellationToken ct) => Ok(await legal.PublishAsync(key, version, ct), "Published.");

    /// <summary>CEO only: marks the version reviewed by the lawyer (the "Draft" note disappears).</summary>
    [HttpPost("{version:int}/lawyer-reviewed")]
    [Authorize(Policy = Policies.Ceo)]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reviewed(string key, int version, LawyerReviewRequest request, CancellationToken ct) =>
        Ok(await legal.LawyerReviewedAsync(key, version, request, ct), "Marked as reviewed by the lawyer.");
}
