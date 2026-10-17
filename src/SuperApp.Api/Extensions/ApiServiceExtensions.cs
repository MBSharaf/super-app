using Microsoft.AspNetCore.Mvc;
using SuperApp.Api.Errors;
using SuperApp.Api.Filters;
using SuperApp.Api.Security;
using SuperApp.Api.Services;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Options;

namespace SuperApp.Api.Extensions;

/// <summary>Registers the Api layer: controllers, errors, security, limits, health checks and the API reference.</summary>
public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<IRequestInfo, RequestInfo>();
        services.Configure<SiteOptions>(configuration.GetSection(SiteOptions.Section));

        services.AddControllers(o =>
        {
            o.Filters.Add<AntiforgeryFilter>();
            o.Filters.Add<ValidationFilter>();
        });
        // Invalid JSON / model binding errors go through the same unified 400 response
        services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ValidationResponse.FromModelState);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddSuperAppAuth();
        services.AddSuperAppRateLimits(configuration);
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddSqlServer(configuration.GetConnectionString("Default")!, name: "database", tags: ["ready"]);
        return services;
    }
}
