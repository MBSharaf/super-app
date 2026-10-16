using Microsoft.AspNetCore.Mvc;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Api.Errors;

/// <summary>Turns ASP.NET model-binding errors (bad JSON, wrong types) into the unified 400 response.</summary>
public static class ValidationResponse
{
    public static IActionResult FromModelState(ActionContext context)
    {
        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => string.IsNullOrEmpty(e.Key) ? "request" : char.ToLowerInvariant(e.Key[0]) + e.Key[1..],
                e => e.Value!.Errors.Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "Invalid value." : x.ErrorMessage).ToArray());
        return new BadRequestObjectResult(ApiResponse.Fail("Please check the highlighted fields.", errors, context.HttpContext.TraceIdentifier));
    }
}
