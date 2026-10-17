using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using Serilog;
using SuperApp.Api.Middlewares;
using SuperApp.Api.Site;
using SuperApp.Application.Common.Responses;

namespace SuperApp.Api.Extensions;

/// <summary>The request pipeline, in order (STRUCTURE §2.3). Order matters: errors first, then ids, headers, auth, limits.</summary>
public static class PipelineExtensions
{
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }
        app.UseHttpsRedirection();

        // Site files first, then routing — so the page fallback route never hides a real css/js file
        app.UseSuperAppSite();
        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();
        app.UseAntiforgery();
        app.UseRateLimiter();
        app.UseOutputCache();

        app.MapControllers();
        // Unknown API addresses answer in the unified shape too, not an empty 404
        app.Map("/api/{**path}", (HttpContext http) => Results.Json(
            ApiResponse.Fail("This address does not exist.", new Dictionary<string, string[]> { ["code"] = ["not_found"] }, http.TraceIdentifier),
            statusCode: StatusCodes.Status404NotFound)).ExcludeFromDescription();
        app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
        if (app.Configuration.GetValue("Docs:Enabled", app.Environment.IsDevelopment()))
        {
            app.MapOpenApi();
            app.MapScalarApiReference("/docs", o => o.WithTitle("Super App API"));
        }
        return app;
    }
}
