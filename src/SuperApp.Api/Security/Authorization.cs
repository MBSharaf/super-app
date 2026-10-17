using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Security;
using SuperApp.Infrastructure.Identity;

namespace SuperApp.Api.Security;

/// <summary>
/// Who may call what. <c>[RequirePermission("approvals.partners")]</c> on an admin endpoint, <c>[Authorize(Policy = Policies.Ceo)]</c>
/// on CEO endpoints. Every admin and CEO endpoint also needs a session that passed the authenticator code (2FA);
/// without it the answer is 403 with code <c>2fa_required</c>.
/// </summary>
public static class Policies
{
    public const string Ceo = "ceo";
    public const string Staff = "staff";
    public const string PermissionPrefix = "perm:";

    public static IServiceCollection AddSuperAppAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(o =>
        {
            o.AddPolicy(Ceo, p => p.RequireRole(SystemRoles.Ceo).AddRequirements(new TwoFactorRequirement()));
            o.AddPolicy(Staff, p => p.RequireClaim(AppClaims.Staff, "1").AddRequirements(new TwoFactorRequirement()));
        });
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, TwoFactorHandler>();
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, UnifiedAuthorizationResultHandler>();
        return services;
    }
}

/// <summary>Requires the permission code on an endpoint (API §1.7 "Admin perm").</summary>
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(Policies.PermissionPrefix + permission);

public sealed record PermissionRequirement(string Code) : IAuthorizationRequirement;

public sealed record TwoFactorRequirement : IAuthorizationRequirement;

/// <summary>Builds "perm:x" policies on the fly: the permission plus the 2FA session.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Policies.PermissionPrefix, StringComparison.Ordinal))
        {
            return await base.GetPolicyAsync(policyName);
        }
        var code = policyName[Policies.PermissionPrefix.Length..];
        if (!Permissions.Exists(code))
        {
            throw new InvalidOperationException($"Unknown permission '{code}' on an endpoint.");
        }
        return new AuthorizationPolicyBuilder().RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(code), new TwoFactorRequirement()).Build();
    }
}

public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(AppClaims.Permission, requirement.Code))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

/// <summary>Passes only when the session was completed with the authenticator code (amr = mfa).</summary>
public sealed class TwoFactorHandler : AuthorizationHandler<TwoFactorRequirement>
{
    public const string FailureCode = "2fa_required";

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TwoFactorRequirement requirement)
    {
        // Not calling Fail() keeps the list of failed requirements, which tells "no 2FA" apart from "no permission"
        if (context.User.HasClaim(AppClaims.AuthMethod, "mfa"))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

/// <summary>401 / 403 answers in the unified shape; a missing 2FA step gets its own code so the site can ask for it.</summary>
public sealed class UnifiedAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden || context.User.Identity?.IsAuthenticated != true)
        {
            await fallback.HandleAsync(next, context, policy, authorizeResult);
            return;
        }
        var failed = authorizeResult.AuthorizationFailure?.FailedRequirements.ToList() ?? [];
        var needs2fa = failed.Count > 0 && failed.All(r => r is TwoFactorRequirement);
        var (message, code) = needs2fa
            ? ("Set up or enter your authenticator code to open this page.", TwoFactorHandler.FailureCode)
            : ("You are not allowed to do this.", "forbidden");
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(ApiResponse.Fail(message, new Dictionary<string, string[]> { ["code"] = [code] }, context.TraceIdentifier));
    }
}
