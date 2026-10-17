using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using SuperApp.Api.Errors;
using SuperApp.Application.Common.Exceptions;

namespace SuperApp.Tests.Api;

/// <summary>Each exception type becomes the right HTTP status in the unified shape, and server errors hide details.</summary>
public sealed class GlobalExceptionHandlerTests
{
    public static TheoryData<Exception, int, string> Cases => new()
    {
        { new ValidationException([new ValidationFailure("Email", "Email is required.")]), 400, "email" },
        { new UnauthorizedException(), 401, "code" },
        { new ForbiddenException(), 403, "code" },
        { new NotFoundException(), 404, "code" },
        { new ConflictException("This hour was just booked.", "slot_taken"), 409, "code" },
        { new BusinessRuleException("Discount above the maximum."), 422, "code" },
        { new FeatureDisabledException(), 423, "code" },
        { new InvalidOperationException("secret connection string here"), 500, "code" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Maps_exception_to_status_and_unified_body(Exception exception, int status, string errorKey)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var handled = await new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance).TryHandleAsync(context, exception, default);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(status);
        context.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(context.Response.Body);
        json.RootElement.GetProperty("success").GetBoolean().ShouldBeFalse();
        json.RootElement.GetProperty("errors").TryGetProperty(errorKey, out _).ShouldBeTrue();
        json.RootElement.GetRawText().ShouldNotContain("secret connection string");
    }
}
