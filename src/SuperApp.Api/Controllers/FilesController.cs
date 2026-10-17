using Microsoft.AspNetCore.Mvc;
using SuperApp.Application.Features.Files;

namespace SuperApp.Api.Controllers;

/// <summary>Downloads a stored file. Private files (ID cards) only for their owner and ID checkers — anyone else gets 404.</summary>
[Route("api/v1/files")]
public sealed class FilesController(IFilesService files) : ApiControllerBase
{
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, CancellationToken ct)
    {
        var file = await files.OpenAsync(id, ct);
        Response.Headers.CacheControl = file.IsPrivate ? "private, no-store" : "public, max-age=86400";
        return File(file.Content, file.ContentType, file.IsPrivate ? file.FileName : null);
    }
}
