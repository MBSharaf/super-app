using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.SignalR;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Communities;
using SuperApp.Application.Features.Support;
using SuperApp.Api.Services;

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

/// <summary>
/// Live support chat (API §9.4): the ticket's owner or a team member with support.read joins the ticket's group and gets
/// <c>message</c>, <c>read</c>, <c>statusChanged</c>, <c>assigned</c>; <c>Typing(code)</c> tells the other side.
/// </summary>
[Authorize]
public sealed class SupportHub(ISupportRepository tickets) : Hub
{
    public const string Path = "/hubs/support";

    public static string Group(string code) => "ticket:" + code.ToUpperInvariant();

    public async Task JoinTicket(string code)
    {
        await AllowedAsync(code);
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(code), Context.ConnectionAborted);
    }

    public async Task Typing(string code)
    {
        var team = await AllowedAsync(code);
        await Clients.OthersInGroup(Group(code)).SendAsync("typing", new { team }, Context.ConnectionAborted);
    }

    /// <summary>True for the team; throws for anyone who is neither the owner nor the team.</summary>
    private async Task<bool> AllowedAsync(string code)
    {
        var team = Context.User?.HasClaim(CurrentUser.PermissionClaim, "support.read") == true;
        var ticket = await tickets.TicketAsync(code.ToUpperInvariant(), Context.ConnectionAborted);
        var me = Guid.TryParse(Context.UserIdentifier, out var id) ? id : Guid.Empty;
        return ticket is not null && (team || ticket.OpenedById == me) ? team : throw new HubException("Ticket not found.");
    }
}

/// <summary>Sends chat events to the ticket's SignalR group.</summary>
public sealed class SignalRSupportLive(IHubContext<SupportHub> hub) : ISupportLive
{
    public Task PublishAsync(string ticketCode, string eventName, object payload, CancellationToken ct) =>
        hub.Clients.Group(SupportHub.Group(ticketCode)).SendAsync(eventName, payload, ct);
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
        services.AddScoped<ISupportLive, SignalRSupportLive>();
        return services;
    }

    public static WebApplication MapFantasyHubs(this WebApplication app)
    {
        app.MapHub<CommunityHub>(CommunityHub.Path);
        app.MapHub<SupportHub>(SupportHub.Path);
        return app;
    }
}
