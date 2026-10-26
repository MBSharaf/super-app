using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;
using SuperApp.Api.Extensions;

namespace SuperApp.Api.Filters;

/// <summary>
/// After a successful change by the team or a venue (any non-GET under <c>/admin</c>, <c>/ceo</c> or <c>/partner</c>:
/// news, content, legal, pitches, fantasy…), the guests' 60-second copy of public reads is dropped, so what was just
/// published shows at once. Customers' own actions do not touch it.
/// </summary>
public sealed class PublicCacheEvictionFilter(IOutputCacheStore cache) : IAsyncActionFilter
{
    private static readonly string[] Areas = ["/api/v1/admin", "/api/v1/ceo", "/api/v1/partner"];

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();
        var request = context.HttpContext.Request;
        if (HttpMethods.IsGet(request.Method) || executed.Exception is { } && !executed.ExceptionHandled
            || !Areas.Any(a => request.Path.StartsWithSegments(a, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        await cache.EvictByTagAsync(PublicCache.Read, context.HttpContext.RequestAborted);
    }
}
