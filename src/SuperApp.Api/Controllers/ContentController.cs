using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Content;

namespace SuperApp.Api.Controllers;

/// <summary>Public site content (API §6.6): home banners and announcement, pop-ups, legal documents, cookie consent, help center, SEO files.</summary>
[Route("api/v1")]
public sealed class ContentController(IContentService content, ILegalHelpService legal) : ApiControllerBase
{
    [HttpGet("content/home")]
    public async Task<ActionResult<object>> Home(CancellationToken ct) => Ok(await content.HomeAsync(ct));

    /// <summary>Active pop-ups and their timing (pop-ups marked "hide from sellers" are left out for sellers).</summary>
    [HttpGet("content/popups")]
    public async Task<ActionResult<object>> Popups(CancellationToken ct) => Ok(await content.PopupsAsync(ct));

    /// <summary>{ kind: shown | clicked } — counted straight in the database.</summary>
    [HttpPost("content/popups/{id:long}/event")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> PopupEvent(long id, PopupEventRequest request, CancellationToken ct)
    {
        await content.CountPopupAsync(id, request, ct);
        return Ok<object?>(null);
    }

    [HttpPost("consents/cookies")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Cookies(CookieConsentRequest request, CancellationToken ct)
    {
        await content.CookieConsentAsync(request, ct);
        return Ok<object?>(null, "Cookie choice saved.");
    }

    [HttpGet("legal")]
    public async Task<ActionResult<object>> Legal(CancellationToken ct) => Ok(await legal.DocumentsAsync(ct));

    [HttpGet("legal/{key}")]
    public async Task<ActionResult<object>> LegalDocument(string key, CancellationToken ct) => Ok(await legal.DocumentAsync(key, ct));

    /// <summary>Records the acceptance of the current version (with the IP).</summary>
    [Authorize]
    [HttpPost("legal/{key}/accept")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Accept(string key, AcceptLegalRequest request, CancellationToken ct)
    {
        await legal.AcceptAsync(key, request, ct);
        return Ok<object?>(null, "Accepted.");
    }

    [HttpGet("help/topics")]
    public async Task<ActionResult<object>> HelpTopics(CancellationToken ct) => Ok(await legal.TopicsAsync(ct));

    [HttpGet("help/articles")]
    public async Task<ActionResult<object>> HelpArticles([FromQuery] HelpQuery query, CancellationToken ct) => Ok(await legal.ArticlesAsync(query, false, ct));

    [HttpPost("help/articles/{id:long}/vote")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Vote(long id, HelpVoteRequest request, CancellationToken ct) => Ok(await legal.VoteAsync(id, request, ct), "Thanks for telling us.");

    /// <summary>Sitemap from live data (public pages, live pitches, news, public communities).</summary>
    [HttpGet("seo/sitemap.xml")]
    public async Task<IActionResult> Sitemap(CancellationToken ct) => Content(await content.SitemapAsync(ct), "application/xml");

    [HttpGet("seo/robots.txt")]
    public IActionResult Robots() => Content(content.Robots(), "text/plain");
}
