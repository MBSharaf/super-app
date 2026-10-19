using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SuperApp.Api.Security;
using SuperApp.Application.Features.AdminConsole;
using SuperApp.Application.Features.CeoSystem;

namespace SuperApp.Api.Controllers.Ceo;

/// <summary>CEO reports (Excel with a dashboard sheet) and the company audit log (API §8). CEO only, 2FA.</summary>
[Route("api/v1/ceo")]
[Authorize(Policy = Policies.Ceo)]
public sealed class CeoReportsController(IAdminOpsService ops) : ApiControllerBase
{
    /// <summary>operations · customers · partners · sellers · support · agents (?from&amp;to); finance / marketing → 422 until Phase 5.</summary>
    [HttpGet("reports/{kind}")]
    public async Task<IActionResult> Report(string kind, [FromQuery] ReportQuery query, CancellationToken ct)
    {
        var file = await ops.ReportAsync(kind, query, ct);
        return File(file.Content, ExcelFile.ContentType, file.FileName);
    }

    [HttpGet("audit")]
    public async Task<ActionResult<object>> Audit([FromQuery] AuditQuery query, CancellationToken ct) => OkPage(await ops.AuditAsync(query, ct));
}
