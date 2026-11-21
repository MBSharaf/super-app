using Microsoft.AspNetCore.Mvc;
using SuperApp.Api.Security;
using SuperApp.Application.Features.AdminConsole;
using SuperApp.Application.Features.CeoSystem;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>
/// Pitch accounts: per pitch its payment mode (deposit or full payment in the app), the money paid in the app and at the
/// pitch, Super Malaab's income and the venue's share; per partner what is held, available, in payout and paid out.
/// </summary>
[Route("api/v1/admin/settlements")]
[RequirePermission("payouts.read")]
public sealed class AdminSettlementsController(ISettlementsService settlements) : ApiControllerBase
{
    /// <summary>?from&amp;to match dates (default: the last 30 days) &amp;q partner, venue or pitch name.</summary>
    [HttpGet]
    public async Task<ActionResult<object>> Get([FromQuery] SettlementsQuery query, CancellationToken ct) => Ok(await settlements.GetAsync(query, ct));

    /// <summary>The same as an Excel file: dashboard sheet, then pitches and partners.</summary>
    [HttpGet("export.xlsx")]
    [RequirePermission("analytics.export")]
    public async Task<IActionResult> Export([FromQuery] SettlementsQuery query, CancellationToken ct)
    {
        var file = await settlements.ExportAsync(query, ct);
        return File(file.Content, ExcelFile.ContentType, file.FileName);
    }
}
