using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Shop.Returns;
using SuperApp.Application.Features.Shop.Stores;
using SuperApp.Domain.Enums;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>Customer returns (API §4.3): what can be returned, request, my returns.</summary>
[Route("api/v1")]
[Authorize]
public sealed class ReturnsController(IReturnsService returns) : ApiControllerBase
{
    [HttpGet("orders/{code}/returnable")]
    public async Task<ActionResult<object>> Returnable(string code, CancellationToken ct) => Ok(await returns.ReturnableAsync(code, ct));

    /// <summary>422 outside_window, printed_defect_only, action_not_allowed, qty_left, one_store_per_return.</summary>
    [HttpPost("returns")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> RequestReturn(ReturnRequestDto request, CancellationToken ct) => Created(await returns.RequestAsync(request, ct), "Return requested.");

    [HttpGet("returns/mine")]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await returns.MineAsync(ct));

    [HttpGet("returns/{code}")]
    public async Task<ActionResult<object>> Get(string code, CancellationToken ct) => Ok(await returns.GetAsync(code, ct));
}

/// <summary>Deciding returns: the same steps for sellers, pitch partners and Super App (approve books the pickup; received refunds, exchanges or reprints).</summary>
public abstract class ReturnDecisionsControllerBase(IReturnDecisionsService decisions) : ApiControllerBase
{
    protected abstract Task<ReturnScope> ScopeAsync(CancellationToken ct);

    /// <summary>?status=Requested|Approved|…</summary>
    [HttpGet("returns")]
    public async Task<ActionResult<object>> List([FromQuery] string? status, CancellationToken ct) => Ok(await decisions.ListAsync(await ScopeAsync(ct), status, ct));

    [HttpPost("returns/{code}/approve")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Approve(string code, CancellationToken ct) => Ok(await decisions.ApproveAsync(await ScopeAsync(ct), code, ct), "Approved. The courier pickup is booked.");

    [HttpPost("returns/{code}/reject")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Reject(string code, RejectReturnRequest request, CancellationToken ct) =>
        Ok(await decisions.RejectAsync(await ScopeAsync(ct), code, request, ct), "Rejected. The customer was told.");

    [HttpPost("returns/{code}/received")]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Received(string code, CancellationToken ct) => Ok(await decisions.ReceivedAsync(await ScopeAsync(ct), code, ct), "Done.");
}

[Route("api/v1/seller")]
[Authorize]
public sealed class SellerReturnsController(IStoreScopes scopes, IReturnDecisionsService decisions) : ReturnDecisionsControllerBase(decisions)
{
    protected override async Task<ReturnScope> ScopeAsync(CancellationToken ct)
    {
        var s = await scopes.SellerAsync(false, ct);
        return new ReturnScope(s.StoreId, s.Name, AuditSystem.Seller);
    }
}

[Route("api/v1/partner")]
[Authorize]
public sealed class PartnerReturnsController(IStoreScopes scopes, IReturnDecisionsService decisions) : ReturnDecisionsControllerBase(decisions)
{
    protected override async Task<ReturnScope> ScopeAsync(CancellationToken ct)
    {
        var s = await scopes.PartnerAsync(ct);
        return new ReturnScope(s.StoreId, s.Name, AuditSystem.Partner);
    }
}

/// <summary>Official store and printed items (returns.manage + 2FA).</summary>
[Route("api/v1/admin")]
[RequirePermission("returns.manage")]
public sealed class AdminReturnsController(IReturnDecisionsService decisions) : ReturnDecisionsControllerBase(decisions)
{
    protected override Task<ReturnScope> ScopeAsync(CancellationToken ct) => Task.FromResult(new ReturnScope(null, "Super App", AuditSystem.Admin));
}
