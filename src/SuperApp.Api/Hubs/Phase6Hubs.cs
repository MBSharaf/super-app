using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Communities;

namespace SuperApp.Api.Hubs;

/// <summary>
/// Live community updates (API §9.4): a signed-in member joins a community's group and receives <c>post</c>,
/// <c>comment</c>, <c>reaction</c> and <c>hidden</c>. Sellers-only communities check the audience like the API does.
/// </summary>
[Authorize]
public sealed class CommunityHub(ICommunitiesService communities) : Hub
{
    public const string Path = "/hubs/community";

    public static string Group(string slug) => "community:" + slug.ToLowerInvariant();

    public async Task JoinCommunity(string slug)
    {
        await communities.GetAsync(slug, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(slug), Context.ConnectionAborted);
    }

    public Task LeaveCommunity(string slug) => Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(slug), Context.ConnectionAborted);
}

/// <summary>Sends community events to the community's SignalR group.</summary>
public sealed class SignalRCommunityLive(IHubContext<CommunityHub> hub) : ICommunityLive
{
    public Task PublishAsync(string communitySlug, string eventName, object payload, CancellationToken ct) =>
        hub.Clients.Group(CommunityHub.Group(communitySlug)).SendAsync(eventName, payload, ct);
}

/// <summary>Slow mode → 429 in the unified shape, with Retry-After (the shared error handler has no 429 case for services yet).</summary>
public sealed class SlowModeFilterAttribute : ExceptionFilterAttribute
{
    public override void OnException(ExceptionContext context)
    {
        if (context.Exception is not SlowModeException slow)
        {
            return;
        }
        context.HttpContext.Response.Headers.RetryAfter = slow.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        context.Result = new ObjectResult(ApiResponse.Fail(slow.Message, new Dictionary<string, string[]> { ["code"] = [slow.Code] }, context.HttpContext.TraceIdentifier))
        {
            StatusCode = StatusCodes.Status429TooManyRequests,
        };
        context.ExceptionHandled = true;
    }
}

/// <summary>Phase 6 additions to the Api layer — one call each from <c>AddApiLayer</c> and <c>UseApiPipeline</c>.</summary>
public static class Phase6Api
{
    public static IServiceCollection AddFantasyApi(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<ICommunityLive, SignalRCommunityLive>();
        return services;
    }

    public static WebApplication MapFantasyHubs(this WebApplication app)
    {
        app.MapHub<CommunityHub>(CommunityHub.Path);
        return app;
    }
}
