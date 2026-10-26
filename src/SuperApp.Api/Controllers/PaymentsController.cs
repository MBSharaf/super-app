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
