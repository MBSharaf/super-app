using System.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Api.Errors;

/// <summary>
/// The one place that turns exceptions into the unified error response with the correct HTTP status
/// (STRUCTURE §2.2). Unexpected errors are logged with the trace id and answered with a generic message —
/// stack traces never reach the client.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        var (status, body) = Map(exception, traceId);
        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled error {TraceId} on {Method} {Path}", traceId, httpContext.Request.Method, httpContext.Request.Path);
        }
        if (exception is TooManyRequestsException wait)
        {
            httpContext.Response.Headers.RetryAfter = wait.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);
        return true;
    }

    private static (int Status, ApiResponse<object> Body) Map(Exception exception, string traceId) => exception switch
    {
        ValidationException v => (StatusCodes.Status400BadRequest,
            ApiResponse.Fail("Please check the highlighted fields.", GroupErrors(v), traceId)),
        BadRequestException e => (StatusCodes.Status400BadRequest, WithCode(e, traceId)),
        UnauthorizedException e => (StatusCodes.Status401Unauthorized, WithCode(e, traceId)),
        ForbiddenException e => (StatusCodes.Status403Forbidden, WithCode(e, traceId)),
        NotFoundException e => (StatusCodes.Status404NotFound, WithCode(e, traceId)),
        ConflictException e => (StatusCodes.Status409Conflict, WithCode(e, traceId)),
        BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, WithCode(e, traceId)),
        FeatureDisabledException e => (StatusCodes.Status423Locked, WithCode(e, traceId)),
        LockedException e => (StatusCodes.Status423Locked, WithCode(e, traceId)),
        TooManyRequestsException e => (StatusCodes.Status429TooManyRequests, WithCode(e, traceId)),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
            ApiResponse.Fail("Someone else changed this a moment ago. Refresh and try again.", Code("stale"), traceId)),
        BadHttpRequestException => (StatusCodes.Status400BadRequest,
            ApiResponse.Fail("The request could not be read.", Code("bad_request"), traceId)),
        _ => (StatusCodes.Status500InternalServerError,
            ApiResponse.Fail("Something went wrong on our side. Please try again.", Code("server_error"), traceId)),
    };

    private static ApiResponse<object> WithCode(AppException e, string traceId) => ApiResponse.Fail(e.Message, Code(e.Code), traceId);

    private static Dictionary<string, string[]> Code(string code) => new() { ["code"] = [code] };

    private static Dictionary<string, string[]> GroupErrors(ValidationException v) => v.Errors
        .GroupBy(e => string.IsNullOrEmpty(e.PropertyName) ? "request" : FieldName(e.PropertyName))
        .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

    /// <summary>"OpeningHours[0].CloseHour" → "openingHours[0].closeHour", as the JSON names the fields.</summary>
    private static string FieldName(string path) =>
        string.Join('.', path.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
}
