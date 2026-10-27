using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Money;

namespace SuperApp.Api.Controllers;

/// <summary>My wallets (customer, partner bookings…), their movements and my receipts (API §5.2).</summary>
[Route("api/v1")]
[Authorize]
public sealed class WalletController(IWalletService wallet, IWalletTopUpService topUps) : ApiControllerBase
{
    [HttpGet("wallet")]
    public async Task<ActionResult<object>> Wallets(CancellationToken ct) => Ok(await wallet.WalletsAsync(ct));

    /// <summary>?type=Customer|PartnerBookings|… &amp;page &amp;pageSize</summary>
    [HttpGet("wallet/ledger")]
    public async Task<ActionResult<object>> Ledger([FromQuery] string? type, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        OkPage(await wallet.LedgerAsync(type, page, pageSize, ct));

    /// <summary>
    /// { amount } (whole EGP, 50–20,000) + header Idempotency-Key. Simulated gateway: in the wallet at once (Paid);
    /// Paymob: Pending with paymentUrl, and the money arrives with the gateway's callback.
    /// </summary>
    [HttpPost("wallet/top-ups")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> TopUp(TopUpRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct) =>
        Ok(await topUps.TopUpAsync(request, idempotencyKey, ct));

    [HttpGet("receipts")]
    public async Task<ActionResult<object>> Receipts(CancellationToken ct) => Ok(await wallet.ReceiptsAsync(ct));

    [HttpGet("receipts/{number}.pdf")]
    public async Task<IActionResult> ReceiptPdf(string number, CancellationToken ct) =>
        File(await wallet.ReceiptPdfAsync(number, ct), "application/pdf", $"{number}.pdf");
}
