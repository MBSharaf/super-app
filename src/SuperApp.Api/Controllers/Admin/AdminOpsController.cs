using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.AdminConsole;
using SuperApp.Application.Features.CeoSystem;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>Overview, orders and bookings, audit log, reports, settings, maintenance and broadcasts (API §7.2).</summary>
[Route("api/v1/admin")]
public sealed class AdminOpsController(IAdminOpsService ops, IPlatformService platform) : ApiControllerBase
{
    /// <summary>Counts for any team member (money figures come with Phase 5).</summary>
    [HttpGet("overview")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<ActionResult<object>> Overview(CancellationToken ct) => Ok(await ops.OverviewAsync(ct));

    [HttpGet("orders")]
    [RequirePermission("orders.read")]
    public async Task<ActionResult<object>> Orders([FromQuery] OrdersQuery query, CancellationToken ct) => OkPage(await ops.OrdersAsync(query, ct));

    [HttpGet("bookings")]
    [RequirePermission("bookings.read")]
    public async Task<ActionResult<object>> Bookings([FromQuery] OrdersQuery query, CancellationToken ct) => OkPage(await ops.BookingsAsync(query, ct));

    [HttpGet("audit")]
    [RequirePermission("audit.read")]
    public async Task<ActionResult<object>> Audit([FromQuery] AuditQuery query, CancellationToken ct) => OkPage(await ops.AuditAsync(query, ct));

    [HttpGet("audit/export.xlsx")]
    [RequirePermission("audit.read")]
    public async Task<IActionResult> AuditExport([FromQuery] AuditQuery query, CancellationToken ct) => Excel(await ops.AuditExportAsync(query, ct));

    /// <summary>operations · customers · partners · sellers · support · agents (?from&amp;to); money reports → 422 until Phase 5.</summary>
    [HttpGet("reports/{kind}.xlsx")]
    [RequirePermission("analytics.export")]
    public async Task<IActionResult> Report(string kind, [FromQuery] ReportQuery query, CancellationToken ct) => Excel(await ops.ReportAsync(kind, query, ct));

    [HttpGet("settings")]
    [RequirePermission("settings.platform")]
    public async Task<ActionResult<object>> Settings(CancellationToken ct) => Ok(await platform.SettingsAsync(null, ct));

    [HttpGet("settings/{group}")]
    [RequirePermission("settings.platform")]
    public async Task<ActionResult<object>> SettingsGroup(string group, CancellationToken ct) => Ok(await platform.SettingsAsync(group, ct));

    /// <summary>Money settings → 403 money_setting.</summary>
    [HttpPut("settings/{group}")]
    [RequirePermission("settings.platform")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> UpdateSettings(string group, UpdateSettingsRequest request, CancellationToken ct) =>
        Ok(await platform.UpdateSettingsAsync(group, request, ct), "Settings saved.");

    [HttpGet("maintenance")]
    [RequirePermission("maintenance.toggle")]
    public async Task<ActionResult<object>> Maintenance(CancellationToken ct) => Ok(await platform.MaintenanceAsync(ct));

    [HttpPut("maintenance")]
    [RequirePermission("maintenance.toggle")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SetMaintenance(MaintenanceRequest request, CancellationToken ct) => Ok(await platform.SetMaintenanceAsync(request, ct), "Saved.");

    [HttpGet("broadcasts")]
    [RequirePermission("broadcasts.send")]
    public async Task<ActionResult<object>> Broadcasts([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        OkPage(await platform.BroadcastsAsync(page, pageSize, ct));

    /// <summary>Now → 201 with the number of people reached; scheduled → 202.</summary>
    [HttpPost("broadcasts")]
    [RequirePermission("broadcasts.send")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Broadcast(BroadcastRequest request, CancellationToken ct)
    {
        var (broadcast, scheduled) = await platform.BroadcastAsync(request, ct);
        return scheduled
            ? StatusCode(StatusCodes.Status202Accepted, Application.Common.Responses.ApiResponse.Ok(broadcast, "Scheduled."))
            : Created(broadcast, $"Sent to {broadcast.Recipients} people.");
    }

    private FileContentResult Excel(ExcelFile file) => File(file.Content, ExcelFile.ContentType, file.FileName);
}
