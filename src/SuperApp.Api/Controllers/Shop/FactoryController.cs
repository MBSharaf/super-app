using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Filters;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Shop.Factory;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>
/// The factory system (API §4.6). The owner works with the account cookie; staff sign in with a PIN on a registered device
/// and send <c>X-Factory-Device</c> and <c>X-Factory-Session</c> with every call. Roles decide each step (403 role).
/// </summary>
[Route("api/v1/factory")]
[PrintOnDemand]
public sealed class FactoryController(IFactoryAuthService auth, IFactoryJobsService jobs, IFactoryOpsService ops, ICatalogueChangesService changes) : ApiControllerBase
{
    /// <summary>The owner (signed in) registers a device once; the token is shown once.</summary>
    [HttpPost("devices/register")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RegisterDevice(RegisterDeviceRequest request, CancellationToken ct) => Created(await auth.RegisterDeviceAsync(request, ct), "Device registered.");

    [HttpGet("staff/list")]
    public async Task<ActionResult<object>> StaffList([FromHeader(Name = "X-Factory-Device")] string? device, CancellationToken ct) => Ok(await auth.StaffListAsync(device ?? "", ct));

    /// <summary>Wrong PIN → 401 bad_pin; 5 wrong → 423 pin_locked for 15 minutes.</summary>
    [HttpPost("auth/pin")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<object>> Pin(PinRequest request, [FromHeader(Name = "X-Factory-Device")] string? device, CancellationToken ct) =>
        Ok(await auth.PinAsync(device ?? "", request, ct), "Signed in.");

    [HttpGet("me")]
    public async Task<ActionResult<object>> Me(CancellationToken ct) => Ok(await auth.MeAsync(ct));

    [HttpGet("overview")]
    public async Task<ActionResult<object>> Overview(CancellationToken ct) => Ok(await jobs.OverviewAsync(ct));

    /// <summary>?stage=active|new|printing|qualitycheck|packing|ready|shipped|cancelled|all &amp;mine &amp;late &amp;q &amp;page &amp;pageSize</summary>
    [HttpGet("jobs")]
    public async Task<ActionResult<object>> Jobs([FromQuery] FactoryJobsQuery query, CancellationToken ct) => OkPage(await jobs.ListAsync(query, ct));

    [HttpGet("jobs/{code}")]
    public async Task<ActionResult<object>> Job(string code, CancellationToken ct) => Ok(await jobs.GetAsync(code, ct));

    /// <summary>The original print file of one side.</summary>
    [HttpGet("jobs/{code}/art/{side:regex(^(front|back)$)}")]
    public async Task<IActionResult> Art(string code, string side, CancellationToken ct)
    {
        var art = await jobs.ArtAsync(code, side, ct);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'";
        return File(art.Bytes, art.ContentType, $"{code}-{side}{(art.ContentType == "image/svg+xml" ? ".svg" : art.ContentType == "image/jpeg" ? ".jpg" : ".png")}");
    }

    /// <summary>{ toStage: Printing | QualityCheck | Packing | Ready }. Wrong stage → 409; role → 403.</summary>
    [HttpPost("jobs/{code}/advance")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Advance(string code, AdvanceRequest request, CancellationToken ct) => Ok(await jobs.AdvanceAsync(code, request, ct));

    /// <summary>A failed check (reason required) sends the job back to printing.</summary>
    [HttpPost("jobs/{code}/qc")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Qc(string code, QcRequest request, CancellationToken ct) => Ok(await jobs.QcAsync(code, request, ct));

    [HttpPost("jobs/{code}/problem")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Problem(string code, ProblemRequest request, CancellationToken ct) => Ok(await jobs.ProblemAsync(code, request, ct), "Reported. The supervisor and Super App were told.");

    [HttpPost("jobs/{code}/assign")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Assign(string code, AssignRequest request, CancellationToken ct) => Ok(await jobs.AssignAsync(code, request, ct));

    [HttpGet("jobs/{code}/ticket.pdf")]
    public async Task<IActionResult> Ticket(string code, CancellationToken ct) => File(await ops.TicketAsync(code, ct), "application/pdf", $"ticket-{code}.pdf");

    [HttpPost("scan")]
    [EnableRateLimiting(RateLimitPolicies.Scan)]
    public async Task<ActionResult<object>> Scan(ScanRequest request, CancellationToken ct) => Ok(await jobs.ScanAsync(request, ct));

    /// <summary>?status=transit|delivered|refused (packer, supervisor, owner).</summary>
    [HttpGet("shipping")]
    public async Task<ActionResult<object>> Shipping([FromQuery] string? status, CancellationToken ct) => Ok(await ops.ShippingAsync(status, ct));

    [HttpGet("shipments/{id:long}/label.pdf")]
    public async Task<IActionResult> Label(long id, CancellationToken ct) => File(await ops.LabelAsync(id, ct), "application/pdf", $"label-{id}.pdf");

    [HttpGet("team")]
    public async Task<ActionResult<object>> Team(CancellationToken ct) => Ok(await ops.TeamAsync(ct));

    [HttpPost("team")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AddStaff(StaffRequest request, CancellationToken ct) => Created(await ops.AddStaffAsync(request, ct), "Team member added.");

    [HttpPut("team/{id:long}")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> EditStaff(long id, StaffRequest request, CancellationToken ct) => Ok(await ops.UpdateStaffAsync(id, request, ct), "Saved.");

    [HttpPost("team/{id:long}/reset-pin")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ResetPin(long id, ResetPinRequest request, CancellationToken ct)
    {
        await ops.ResetPinAsync(id, request, ct);
        return Ok<object?>(null, "PIN reset.");
    }

    [HttpGet("money")]
    public async Task<ActionResult<object>> Money(CancellationToken ct) => Ok(await ops.MoneyAsync(ct));

    [HttpGet("money/statement.xlsx")]
    public async Task<IActionResult> Statement(CancellationToken ct)
    {
        var (file, name) = await ops.StatementAsync(ct);
        return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [HttpGet("reports")]
    public async Task<ActionResult<object>> Reports([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) => Ok(await ops.ReportAsync(from, to, ct));

    [HttpGet("settings")]
    public async Task<ActionResult<object>> Settings(CancellationToken ct) => Ok(await ops.SettingsAsync(ct));

    /// <summary>Supervisors may only pause / open intake; the owner also sets capacity and SLA. Paused intake hides printed items from the shop.</summary>
    [HttpPut("settings")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SaveSettings(FactorySettingsRequest request, CancellationToken ct) => Ok(await ops.SaveSettingsAsync(request, ct), "Saved.");

    /// <summary>Waits for admin approval; a pending request of the same kind on the product → 409 pending_change.</summary>
    [HttpPost("catalogue-changes")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RequestChange(CatalogueChangeRequest request, CancellationToken ct) => Created(await changes.RequestAsync(request, ct), "Sent for approval.");

    [HttpGet("catalogue-changes")]
    public async Task<ActionResult<object>> Changes(CancellationToken ct) => Ok(await changes.MineAsync(ct));
}

/// <summary>Admins over the factories (API §7.2 "Factories", approvals.catalogue for change requests).</summary>
[Route("api/v1/admin")]
[PrintOnDemand]
public sealed class AdminFactoryController(IAdminFactoryService factories, ICatalogueChangesService changes) : ApiControllerBase
{
    [HttpGet("factories")]
    [RequirePermission("factories.read")]
    public async Task<ActionResult<object>> Factories(CancellationToken ct) => Ok(await factories.FactoriesAsync(ct));

    /// <summary>?late &amp;problem &amp;factoryId &amp;page &amp;pageSize</summary>
    [HttpGet("jobs")]
    [RequirePermission("factories.read")]
    public async Task<ActionResult<object>> Jobs([FromQuery] AdminJobsQuery query, CancellationToken ct) => OkPage(await factories.JobsAsync(query, ct));

    [HttpPost("jobs/{code}/reassign")]
    [RequirePermission("factories.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reassign(string code, ReassignRequest request, CancellationToken ct)
    {
        await factories.ReassignAsync(code, request, ct);
        return Ok<object?>(null, "Moved.");
    }

    [HttpPost("jobs/{code}/cancel")]
    [RequirePermission("factories.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Cancel(string code, ResolveRequest request, CancellationToken ct)
    {
        await factories.CancelAsync(code, request, ct);
        return Ok<object?>(null, "Cancelled. The customer was told.");
    }

    [HttpPost("jobs/{code}/reprint")]
    [RequirePermission("factories.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reprint(string code, ReprintRequest request, CancellationToken ct)
    {
        await factories.ReprintAsync(code, request, ct);
        return Ok<object?>(null, "Sent for reprint.");
    }

    [HttpPost("jobs/problems/{id:long}/resolve")]
    [RequirePermission("factories.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Resolve(long id, ResolveRequest request, CancellationToken ct)
    {
        await factories.ResolveProblemAsync(id, request, ct);
        return Ok<object?>(null, "Resolved.");
    }

    /// <summary>{ "status": "Active" | "PausedByAdmin" | "Suspended" }</summary>
    [HttpPost("factories/{id:long}/status")]
    [RequirePermission("factories.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Status(long id, FactoryStatusRequest request, CancellationToken ct)
    {
        await factories.SetStatusAsync(id, request.Status, ct);
        return Ok<object?>(null, "Saved.");
    }

    [HttpGet("catalogue-changes")]
    [RequirePermission("approvals.catalogue")]
    public async Task<ActionResult<object>> Changes(CancellationToken ct) => Ok(await changes.PendingAsync(ct));

    [HttpPost("catalogue-changes/{id:long}/approve")]
    [RequirePermission("approvals.catalogue")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Approve(long id, CancellationToken ct)
    {
        await changes.ApproveAsync(id, ct);
        return Ok<object?>(null, "Approved. Sellers using the product were told.");
    }

    [HttpPost("catalogue-changes/{id:long}/reject")]
    [RequirePermission("approvals.catalogue")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reject(long id, ResolveRequest request, CancellationToken ct)
    {
        await changes.RejectAsync(id, request, ct);
        return Ok<object?>(null, "Sent back to the factory.");
    }
}

public sealed record FactoryStatusRequest(string Status);
