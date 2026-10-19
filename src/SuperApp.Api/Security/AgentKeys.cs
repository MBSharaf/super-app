using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Agents;
using SuperApp.Application.Features.CeoSystem;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Identity;

namespace SuperApp.Api.Security;

/// <summary>
/// AI agents and n8n sign in with <c>X-Api-Key: sa_…</c> (API §1.3). The key's agent becomes the caller: its service
/// account, its permission codes (so it can call every admin endpoint it holds the permission for), and a strong-auth
/// mark in place of the 2FA step. Revoked, expired or unknown keys → 401 <c>invalid_api_key</c>; a paused agent → 423
/// <c>agent_paused</c>; the emergency stop (<c>kill.agents</c>) → 423 <c>agents_stopped</c>. Each key has its own limit
/// (default 600 calls a minute) and every agent call is written to the audit log. Requests without the header keep
/// using the site's cookie.
/// </summary>
public static class AgentKeys
{
    public const string Scheme = "ApiKey";
    public const string SmartScheme = "CookieOrApiKey";
    public const string Policy = "agent";
    public const string AgentClaim = "agent_id";
    public const string KeyClaim = "agent_key_id";
    public const string PrefixClaim = "agent_key_prefix";
    public const string RateClaim = "agent_rate";

    /// <summary>Phase 7: one call from <c>ApiServiceExtensions.AddApiLayer</c>.</summary>
    public static IServiceCollection AddAgentKeys(IServiceCollection services)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(Scheme, null)
            .AddPolicyScheme(SmartScheme, null, o => o.ForwardDefaultSelector = ctx =>
                ctx.Request.Headers.ContainsKey(ApiKeys.Header) ? Scheme : IdentityConstants.ApplicationScheme);
        services.PostConfigure<AuthenticationOptions>(o =>
        {
            o.DefaultScheme = SmartScheme;
            o.DefaultAuthenticateScheme = SmartScheme;
            o.DefaultChallengeScheme = SmartScheme;
            o.DefaultForbidScheme = SmartScheme;
        });
        services.Configure<AuthorizationOptions>(o => o.AddPolicy(Policy, p => p.RequireClaim(AgentClaim)));
        services.PostConfigure<RateLimiterOptions>(o => o.GlobalLimiter = new AgentAwareLimiter(o.GlobalLimiter));
        services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(o => o.Filters.Add<AgentCallAuditFilter>());
        services.AddScoped<ICurrentAgent, CurrentAgent>();
        return services;
    }

    /// <summary>True when the request was signed in with an agent key (not the site's cookie).</summary>
    public static bool IsAgent(this ClaimsPrincipal user) => user.Identity?.AuthenticationType == Scheme && user.HasClaim(c => c.Type == AgentClaim);
}

/// <summary>Checks the key and builds the agent's identity.</summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAgentKeyStore keys,
    ISwitchRepository switches,
    IClock clock) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    private const string FailureItem = "agent-key-failure";

    private sealed record Failure(int Status, string Message, string Code);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var key = Request.Headers[ApiKeys.Header].ToString().Trim();
        var identity = ApiKeys.LooksValid(key) ? await keys.FindAsync(ApiKeys.PrefixOf(key), Context.RequestAborted) : null;
        var now = clock.UtcNow;
        if (identity is null || !ApiKeys.Matches(key, identity.Hash) || identity.RevokedAt is not null || identity.ExpiresAt <= now)
        {
            return Deny(StatusCodes.Status401Unauthorized, "This API key is not valid (unknown, revoked or expired).", "invalid_api_key");
        }
        if (!await switches.IsOnAsync(KillSwitches.Agents, Context.RequestAborted) || identity.AgentStatus == nameof(AgentStatus.EmergencyStopped))
        {
            return Deny(StatusCodes.Status423Locked, "Every AI agent is stopped by the CEO for now.", "agents_stopped");
        }
        if (identity.AgentStatus == nameof(AgentStatus.Paused))
        {
            return Deny(StatusCodes.Status423Locked, "This agent is paused. Ask the CEO to resume it.", "agent_paused");
        }
        if (identity.LastUsedAt is null || identity.LastUsedAt < now.AddMinutes(-1))
        {
            await keys.TouchAsync(identity.KeyId, now, Context.RequestAborted);
        }
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(Identity(identity)), Scheme.Name));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        var failure = Context.Items[FailureItem] as Failure ?? new Failure(StatusCodes.Status401Unauthorized, "Send a valid X-Api-Key.", "invalid_api_key");
        Response.StatusCode = failure.Status;
        await Response.WriteAsJsonAsync(ApiResponse.Fail(failure.Message, new Dictionary<string, string[]> { ["code"] = [failure.Code] }, Context.TraceIdentifier));
    }

    private AuthenticateResult Deny(int status, string message, string code)
    {
        Context.Items[FailureItem] = new Failure(status, message, code);
        return AuthenticateResult.Fail(code);
    }

    private ClaimsIdentity Identity(AgentKeyIdentity key)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, key.ServiceUserId.ToString()),
            new(ClaimTypes.Name, "agent-key:" + key.Prefix),
            new(AppClaims.FullName, key.AgentName),
            new(AgentKeys.AgentClaim, key.AgentId.ToString(CultureInfo.InvariantCulture)),
            new(AgentKeys.KeyClaim, key.KeyId.ToString(CultureInfo.InvariantCulture)),
            new(AgentKeys.PrefixClaim, key.Prefix),
            new(AgentKeys.RateClaim, key.RateLimitPerMinute.ToString(CultureInfo.InvariantCulture)),
            new(AppClaims.AuthMethod, "mfa"),
            new(AppClaims.Staff, "1"),
        };
        claims.AddRange(key.Permissions.Select(code => new Claim(AppClaims.Permission, code)));
        return new ClaimsIdentity(claims, Scheme.Name);
    }
}

/// <summary>The agent behind the request, read from the key's claims.</summary>
public sealed class CurrentAgent(IHttpContextAccessor accessor) : ICurrentAgent
{
    public AgentCaller? Caller
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user is null || !user.IsAgent())
            {
                return null;
            }
            return new AgentCaller(long.Parse(user.FindFirstValue(AgentKeys.AgentClaim)!, CultureInfo.InvariantCulture),
                long.Parse(user.FindFirstValue(AgentKeys.KeyClaim)!, CultureInfo.InvariantCulture), user.FindFirstValue(AgentKeys.PrefixClaim)!,
                int.Parse(user.FindFirstValue(AgentKeys.RateClaim)!, CultureInfo.InvariantCulture));
        }
    }
}

/// <summary>Agent keys get their own per-key limit; everyone else keeps the site's global limit.</summary>
public sealed class AgentAwareLimiter(PartitionedRateLimiter<HttpContext>? site) : PartitionedRateLimiter<HttpContext>
{
    private readonly PartitionedRateLimiter<HttpContext> agents = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var prefix = ctx.User.FindFirstValue(AgentKeys.PrefixClaim) ?? "?";
        var rate = int.TryParse(ctx.User.FindFirstValue(AgentKeys.RateClaim), CultureInfo.InvariantCulture, out var r) ? r : ApiKeys.DefaultRatePerMinute;
        return RateLimitPartition.GetFixedWindowLimiter($"agent:{prefix}:{rate}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rate, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
        });
    });

    private readonly PartitionedRateLimiter<HttpContext> none = PartitionedRateLimiter.Create<HttpContext, string>(_ => RateLimitPartition.GetNoLimiter("none"));

    public override RateLimiterStatistics? GetStatistics(HttpContext resource) => Pick(resource).GetStatistics(resource);

    protected override RateLimitLease AttemptAcquireCore(HttpContext resource, int permitCount) => Pick(resource).AttemptAcquire(resource, permitCount);

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(HttpContext resource, int permitCount, CancellationToken cancellationToken) =>
        Pick(resource).AcquireAsync(resource, permitCount, cancellationToken);

    private PartitionedRateLimiter<HttpContext> Pick(HttpContext ctx) => ctx.User.IsAgent() ? agents : site ?? none;
}

/// <summary>Writes one audit line (system ApiOrAgent, the agent and its API group) for every call made with an agent key.</summary>
public sealed class AgentCallAuditFilter(ICurrentAgent agent, IAuditLogger audit) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (agent.Caller is { } caller)
        {
            var request = context.HttpContext.Request;
            var path = request.Path.Value ?? "/";
            await audit.LogAsync(AuditSystem.ApiOrAgent, Infrastructure.Admin.AiPanelReader.AgentCallAction,
                $"{request.Method} {path} (key {caller.KeyPrefix}…)", "ApiGroup", ApiCatalogue.GroupOf(path), ct: context.HttpContext.RequestAborted);
        }
        await next();
    }
}
