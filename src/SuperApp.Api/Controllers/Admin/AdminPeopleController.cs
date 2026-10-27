using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.AdminConsole;

namespace SuperApp.Api.Controllers.Admin;

/// <summary>Customers, partners and sellers (API §7.2). 2FA admin or agent key with the permission shown; every action is audited.</summary>
[Route("api/v1/admin")]
public sealed class AdminPeopleController(IAdminPeopleService people, IAdminOpsService ops) : ApiControllerBase
{
    [HttpGet("customers")]
    [RequirePermission("customers.read")]
    public async Task<ActionResult<object>> Customers([FromQuery] PeopleQuery query, CancellationToken ct) => OkPage(await people.CustomersAsync(query, ct));

    /// <summary>Excel of the customers who joined in ?from&amp;to (dashboard sheet first).</summary>
    [HttpGet("customers/export.xlsx")]
    [RequirePermission("analytics.export")]
    public async Task<IActionResult> ExportCustomers([FromQuery] Application.Features.CeoSystem.ReportQuery query, CancellationToken ct)
    {
        var file = await ops.ReportAsync("customers", query, ct);
        return File(file.Content, Application.Features.CeoSystem.ExcelFile.ContentType, file.FileName);
    }

    [HttpGet("customers/{id:guid}")]
    [RequirePermission("customers.read")]
    public async Task<ActionResult<object>> Customer(Guid id, CancellationToken ct) => Ok(await people.CustomerAsync(id, ct));

    [HttpPost("customers/{id:guid}/block")]
    [RequirePermission("customers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Block(Guid id, ReasonInput request, CancellationToken ct) => Ok(await people.BlockAsync(id, request, ct), "Customer blocked from booking and ordering.");

    [HttpPost("customers/{id:guid}/unblock")]
    [RequirePermission("customers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Unblock(Guid id, CancellationToken ct) => Ok(await people.UnblockAsync(id, ct), "Customer active again.");

    [HttpPost("customers/{id:guid}/prepay")]
    [RequirePermission("customers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Prepay(Guid id, PrepayRequest request, CancellationToken ct) => Ok(await people.PrepayAsync(id, request, ct), "Saved.");

    [HttpPost("customers/{id:guid}/note")]
    [RequirePermission("customers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Note(Guid id, NoteRequest request, CancellationToken ct) => Ok(await people.NoteAsync(id, request, ct), "Note saved.");

    /// <summary>{ amount 5–5,000, reason } to their wallet, paid by Super App; checks the caller's credit limit (403 over_credit_limit).</summary>
    [HttpPost("customers/{id:guid}/credit")]
    [RequirePermission("credits.issue")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Credit(Guid id, CreditRequest request, CancellationToken ct) => Ok(await people.CreditAsync(id, request, ct), "Credit sent.");

    [HttpPost("customers/{id:guid}/points")]
    [RequirePermission("customers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Points(Guid id, PointsRequest request, CancellationToken ct) => Ok(await people.PointsAsync(id, request, ct), "Points updated.");

    [HttpPost("customers/{id:guid}/refusals/reset")]
    [RequirePermission("customers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ResetRefusals(Guid id, CancellationToken ct) => Ok(await people.ResetRefusalsAsync(id, ct), "Refusals reset.");

    [HttpGet("partners")]
    [RequirePermission("partners.read")]
    public async Task<ActionResult<object>> Partners([FromQuery] PeopleQuery query, CancellationToken ct) => OkPage(await people.PartnersAsync(query, ct));

    [HttpGet("partners/{id:long}")]
    [RequirePermission("partners.read")]
    public async Task<ActionResult<object>> Partner(long id, CancellationToken ct) => Ok(await people.PartnerAsync(id, ct));

    [HttpPost("partners/{id:long}/suspend")]
    [RequirePermission("partners.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SuspendPartner(long id, ReasonInput request, CancellationToken ct) =>
        Ok(await people.SetPartnerStatusAsync(id, true, request.Reason, ct), "Partner suspended.");

    [HttpPost("partners/{id:long}/reactivate")]
    [RequirePermission("partners.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ReactivatePartner(long id, CancellationToken ct) => Ok(await people.SetPartnerStatusAsync(id, false, null, ct), "Partner active again.");

    /// <summary>{ bookings, store } percent 0–50, empty = the platform rate; new bookings use it.</summary>
    [HttpPut("partners/{id:long}/commission")]
    [RequirePermission("partners.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Commission(long id, CommissionDealRequest request, CancellationToken ct) =>
        Ok(await people.CommissionAsync(id, request, ct), "Commission saved.");

    /// <summary>{ amount (+ or −), reason } on the bookings wallet; checks the caller's payout limit (403 over_adjust_limit).</summary>
    [HttpPost("partners/{id:long}/adjust")]
    [RequirePermission("payouts.prepare")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> AdjustBalance(long id, BalanceAdjustRequest request, CancellationToken ct) =>
        Ok(await people.AdjustBalanceAsync(id, request, ct), "Balance adjusted.");

    /// <summary>Closes a live pitch for new bookings (the owner sees the reason); answers with the partner drawer.</summary>
    [HttpPost("pitches/{id:long}/close")]
    [RequirePermission("partners.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ClosePitch(long id, ReasonInput request, CancellationToken ct) =>
        Ok(await people.SetPitchClosedAsync(id, true, request.Reason, ct), "Pitch closed.");

    [HttpPost("pitches/{id:long}/reopen")]
    [RequirePermission("partners.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ReopenPitch(long id, CancellationToken ct) => Ok(await people.SetPitchClosedAsync(id, false, null, ct), "Pitch open again.");

    [HttpGet("sellers")]
    [RequirePermission("sellers.read")]
    public async Task<ActionResult<object>> Sellers([FromQuery] PeopleQuery query, CancellationToken ct) => OkPage(await people.SellersAsync(query, ct));

    [HttpGet("sellers/{id:long}")]
    [RequirePermission("sellers.read")]
    public async Task<ActionResult<object>> Seller(long id, CancellationToken ct) => Ok(await people.SellerAsync(id, ct));

    [HttpPost("sellers/{id:long}/suspend")]
    [RequirePermission("sellers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> SuspendSeller(long id, ReasonInput request, CancellationToken ct) =>
        Ok(await people.SetSellerStatusAsync(id, true, request.Reason, ct), "Seller suspended.");

    [HttpPost("sellers/{id:long}/reactivate")]
    [RequirePermission("sellers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> ReactivateSeller(long id, CancellationToken ct) => Ok(await people.SetSellerStatusAsync(id, false, null, ct), "Seller active again.");

    [HttpPost("sellers/{id:long}/feature")]
    [RequirePermission("sellers.manage")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Feature(long id, FeatureRequest request, CancellationToken ct) => Ok(await people.FeatureSellerAsync(id, request, ct), "Saved.");
}
