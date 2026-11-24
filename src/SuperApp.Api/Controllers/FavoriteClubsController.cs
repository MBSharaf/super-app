using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using SuperApp.Api.Extensions;
using SuperApp.Api.Security;
using SuperApp.Application.Features.Favorites;

namespace SuperApp.Api.Controllers;

/// <summary>Favourite clubs (up to three): the picker's list, and the signed-in person's own clubs.</summary>
[Route("api/v1")]
public sealed class FavoriteClubsController(IFavoriteClubsService favorites) : ApiControllerBase
{
    /// <summary>The clubs that can be followed, by competition (current seasons).</summary>
    [HttpGet("matches/clubs")]
    [OutputCache(PolicyName = PublicCache.Read)]
    public async Task<ActionResult<object>> Clubs(CancellationToken ct) => Ok(await favorites.PickableAsync(ct));

    [HttpGet("me/favorite-clubs")]
    [Authorize]
    public async Task<ActionResult<object>> Mine(CancellationToken ct) => Ok(await favorites.MineAsync(ct));

    [HttpPut("me/favorite-clubs")]
    [Authorize]
    [EnableRateLimiting(RateLimitPolicies.Write)]
    public async Task<ActionResult<object>> Set(SetFavoriteClubsRequest request, CancellationToken ct) =>
        Ok(await favorites.SetAsync(request, ct), "Your clubs are saved.");
}
