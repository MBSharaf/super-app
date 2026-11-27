using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Features.Auth.Dtos;
using SuperApp.Application.Features.Auth.Services;

namespace SuperApp.Api.Controllers;

/// <summary>The signed-in person's own account (API §2.2).</summary>
[Route("api/v1/me")]
[Authorize]
public sealed class MeController(IMeService me) : ApiControllerBase
{
    /// <summary>Profile, roles, permissions and the dashboards this person can open.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> Get(CancellationToken ct) => Ok(await me.GetCurrentAsync(ct));

    [HttpPatch]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Update(UpdateMeRequest request, CancellationToken ct) =>
        Ok(await me.UpdateAsync(request, ct), "Saved.");

    /// <summary>The profile photo: multipart field "avatar" (JPEG/PNG/WEBP, up to 5 MB).</summary>
    [HttpPost("avatar")]
    [EnableRateLimiting(RateLimitPolicies.Upload)]
    [RequestSizeLimit(MaxUpload)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUpload)]
    public async Task<ActionResult<object>> SetAvatar([FromForm(Name = "avatar")] IFormFile? file, CancellationToken ct) =>
        Ok(await me.SetAvatarAsync(file is null ? null : new UploadedFile(file.OpenReadStream(), file.FileName, file.ContentType, file.Length), ct), "Photo saved.");

    [HttpDelete("avatar")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RemoveAvatar(CancellationToken ct) => Ok(await me.RemoveAvatarAsync(ct), "Photo removed.");

    private const long MaxUpload = 6 * 1024 * 1024;

    /// <summary>
    /// Deletes the signed-in person's account for good: { confirm: "DELETE", password } (no password for Google-only
    /// accounts). 409 staff_account / business_account / open_bookings / wallet_balance · 400 wrong_password.
    /// </summary>
    [HttpPost("delete")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Delete(DeleteAccountRequest request, [FromServices] AccountDeletionService deletion, CancellationToken ct)
    {
        await deletion.DeleteAsync(request, ct);
        return NoContent();
    }

    [HttpPut("password")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        await me.ChangePasswordAsync(request, ct);
        return NoContent();
    }
}
