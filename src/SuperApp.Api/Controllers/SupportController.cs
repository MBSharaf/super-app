using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Features.Support;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Support tickets and live chat (API §6.4). Customers, partners, sellers and factories open tickets; each is a chat
/// with the team (hub <c>/hubs/support</c>). Opening answers 201 with the auto-reply and sends it by email.
/// </summary>
[Route("api/v1/support/tickets")]
[Authorize]
public sealed class SupportController(ISupportService support) : ApiControllerBase
{
    private const long MaxUpload = 30 * 1024 * 1024;

    /// <summary>Topic, subject, message; optional booking / order code (yours) and a dispute amount.</summary>
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Open(OpenTicketRequest request, CancellationToken ct) =>
        Created(await support.OpenAsync(request, ct), "We got your message. The team replies within 24 hours.");

    [HttpGet("mine")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await support.MineAsync(ct));

    [HttpGet("{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await support.GetAsync(code, ct));

    /// <summary>{ body } — 422 closed.</summary>
    [HttpPost("{code}/messages")]
    [EnableRateLimiting(RateLimitPolicies.Chat)]
    public async Task<ActionResult<object>> Reply(string code, TicketMessageRequest request, CancellationToken ct) =>
        Created(await support.ReplyAsync(code, request, [], ct));

    /// <summary>Multipart: field <c>body</c> and up to 4 files in <c>files</c> (photos or PDF; private to you and the team).</summary>
    [HttpPost("{code}/attachments")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> Attach(string code, [FromForm] string body, [FromForm(Name = "files")] List<IFormFile> files, CancellationToken ct) =>
        Created(await support.ReplyAsync(code, new TicketMessageRequest(body), files.Select(f => new UploadedFile(f.OpenReadStream(), f.FileName, f.ContentType, f.Length)).ToList(), ct));

    [HttpPost("{code}/close")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Close(string code, CancellationToken ct) => Ok(await support.CloseAsync(code, ct), "Ticket closed.");

    /// <summary>Marks the team's messages as read (the team sees "read").</summary>
    [HttpPost("{code}/read")]
    public async Task<ActionResult<object>> Read(string code, CancellationToken ct)
    {
        await support.ReadAsync(code, ct);
        return Ok<object?>(null);
    }
}
