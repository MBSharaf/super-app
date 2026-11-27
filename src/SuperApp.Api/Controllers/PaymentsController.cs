using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Payments;

namespace SuperApp.Api.Controllers;

/// <summary>
/// Payments (API §5.1). The site polls a payment's status after the gateway sends the payer back — the redirect never
/// confirms a payment; only the gateway's signed callback does.
/// </summary>
[Route("api/v1")]
[Authorize]
public sealed class PaymentsController(IPaymentsService payments) : ApiControllerBase
{
    [HttpGet("payments/{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await payments.GetAsync(code, ct));

    /// <summary>A new payment page for an unpaid payment. Paid → 422 already_paid.</summary>
    [HttpPost("payments/{code}/retry")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Retry(string code, CancellationToken ct) => Ok(await payments.RetryAsync(code, ct));

    /// <summary>Manual InstaPay: { reference, sender } — the payer's transaction number for the team to check. 422 payment_closed / payment_expired.</summary>
    [HttpPost("payments/{code}/instapay")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> InstaPaySent(string code, InstaPaySubmitRequest body, [FromServices] InstaPayService instaPay, CancellationToken ct) =>
        Ok(await instaPay.SubmitAsync(code, body, ct), "Sent. We will confirm as soon as we see it.");

    /// <summary>?status=waiting (default) | unsent | all — InstaPay payments for the team to check against the bank app.</summary>
    [HttpGet("admin/instapay")]
    [RequirePermission("payments.confirm")]
    public async Task<ActionResult<object>> InstaPayQueue([FromQuery] string? status, [FromServices] InstaPayService instaPay, CancellationToken ct) =>
        Ok(await instaPay.QueueAsync(status, ct));

    [HttpPost("admin/instapay/{code}/confirm")]
    [RequirePermission("payments.confirm")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> InstaPayConfirm(string code, [FromServices] InstaPayService instaPay, CancellationToken ct) =>
        Ok(await instaPay.ConfirmAsync(code, ct), "Payment confirmed.");

    /// <summary>{ reason } — the money did not arrive; the payer is told.</summary>
    [HttpPost("admin/instapay/{code}/reject")]
    [RequirePermission("payments.confirm")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> InstaPayReject(string code, InstaPayRejectRequest body, [FromServices] InstaPayService instaPay, CancellationToken ct) =>
        Ok(await instaPay.RejectAsync(code, body, ct), "Payment refused.");

    /// <summary>Gateway callback (Paymob: "Transaction processed callback", signed in ?hmac=). Repeats change nothing.</summary>
    [HttpPost("webhooks/payments/{provider}")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Agent)]
    public async Task<ActionResult<object>> Webhook(string provider, CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        var query = Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        await payments.WebhookAsync(provider, query, body, ct);
        return Ok(new { received = true }, "Received.");
    }
}
