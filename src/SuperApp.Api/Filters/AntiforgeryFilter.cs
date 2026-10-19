using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Api.Filters;

/// <summary>
/// Every POST / PUT / PATCH / DELETE must carry the anti-forgery token (header X-CSRF-TOKEN, from GET /auth/csrf),
/// so another website cannot act with the visitor's cookie. Missing or wrong → 400 with code "csrf".
/// Endpoints for machines (webhooks, agent API keys) opt out with [IgnoreAntiforgeryToken].
/// </summary>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
            || context.ActionDescriptor.EndpointMetadata.OfType<IgnoreAntiforgeryTokenAttribute>().Any()
            || Security.AgentKeys.IsAgent(context.HttpContext.User))
        {
            return;
        }
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(ApiResponse.Fail(
                "Your session check failed. Refresh the page and try again.",
                new Dictionary<string, string[]> { ["code"] = ["csrf"] },
                context.HttpContext.TraceIdentifier));
        }
    }
}
