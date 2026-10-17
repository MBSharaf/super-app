using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Features.Applications.Dtos;
using SuperApp.Application.Features.Applications.Services;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Apply to become a pitch partner, seller or factory (API §2.3). Sent as multipart/form-data: <c>answers</c> =
/// the answers as JSON, <c>idFront</c> + <c>idBack</c> = the ID card, <c>photos</c> = venue photos, <c>documents</c> = papers.
/// </summary>
[Route("api/v1/applications")]
[Authorize]
public sealed class ApplicationsController(IApplicationsService applications) : ApiControllerBase
{
    private const long MaxUpload = 40 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The multipart form shared by the three applications.</summary>
    public sealed class ApplicationForm
    {
        public string Answers { get; set; } = "{}";
        public IFormFile? IdFront { get; set; }
        public IFormFile? IdBack { get; set; }
        public List<IFormFile> Photos { get; set; } = [];
        public List<IFormFile> Documents { get; set; } = [];
    }

    [HttpPost("pitch-owner")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> PitchOwner([FromForm] ApplicationForm form, CancellationToken ct) =>
        Created(await applications.ApplyPitchOwnerAsync(Read<PitchOwnerAnswers>(form), Files(form), ct), "Application sent. We review it within 2 working days.");

    [HttpPost("seller")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> Seller([FromForm] ApplicationForm form, CancellationToken ct) =>
        Created(await applications.ApplySellerAsync(Read<SellerAnswers>(form), Files(form), ct), "Application sent. We review it within 2 working days.");

    [HttpPost("factory")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> Factory([FromForm] ApplicationForm form, CancellationToken ct) =>
        Created(await applications.ApplyFactoryAsync(Read<FactoryAnswers>(form), Files(form), ct), "Application sent. We review it within 2 working days.");

    [HttpGet("mine")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await applications.MineAsync(ct));

    /// <summary>Fix and resend a rejected application; files are replaced only when sent again.</summary>
    [HttpPut("{code}")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> Resubmit(string code, [FromForm] ApplicationForm form, CancellationToken ct) =>
        Ok(await applications.ResubmitAsync(code, Read<JsonElement>(form), Files(form), ct), "Application sent again.");

    private static T Read<T>(ApplicationForm form)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(form.Answers, Json) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new ValidationException([new ValidationFailure("answers", "The answers could not be read. Please fill the form again.")]);
        }
    }

    private static ApplicationFiles Files(ApplicationForm form) =>
        new(Upload(form.IdFront), Upload(form.IdBack), form.Photos.Select(f => Upload(f)!).ToList(), form.Documents.Select(f => Upload(f)!).ToList());

    private static UploadedFile? Upload(IFormFile? file) =>
        file is null ? null : new UploadedFile(file.OpenReadStream(), file.FileName, file.ContentType, file.Length);
}
