using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Shop.Catalog;

namespace SuperApp.Api.Controllers.Shop;

/// <summary>Store pages <c>/s/{handle}</c> (API §4.1): the store, its products and designs; customers follow creators' stores.</summary>
[Route("api/v1/stores")]
public sealed class StoresController(IStoresService stores) : ApiControllerBase
{
    /// <summary>The owner also gets designs that are not public yet (draft, in review, rejected, paused) with their status.</summary>
    [HttpGet("{handle}")]
    [AllowAnonymous]
    public async Task<ActionResult<object>> Get(string handle, CancellationToken ct) => Ok(await stores.GetAsync(handle, ct));

    /// <summary>Only creators' stores (422 not_followable); not your own (422 own_store). Returns the follower count.</summary>
    [HttpPost("{handle}/follow")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Follow(string handle, CancellationToken ct) => Ok(new { followers = await stores.FollowAsync(handle, true, ct), following = true }, "Following.");

    [HttpDelete("{handle}/follow")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Unfollow(string handle, CancellationToken ct) => Ok(new { followers = await stores.FollowAsync(handle, false, ct), following = false }, "Unfollowed.");
}
