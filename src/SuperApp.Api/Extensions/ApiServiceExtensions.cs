using Microsoft.AspNetCore.Mvc;
using SuperApp.Api.Errors;
using SuperApp.Api.Filters;
using SuperApp.Api.Security;
using SuperApp.Api.Services;
using SuperApp.Application.Abstractions.Services;

namespace SuperApp.Api.Extensions;

/// <summary>Registers the Api layer: controllers, errors, security, limits, health checks and the API reference.</summary>
public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddControllers(o => o.Filters.Add<ValidationFilter>());
        // Invalid JSON / model binding errors go through the same unified 400 response
        services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = ValidationResponse.FromModelState);

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddSuperAppAuth(configuration);
        services.AddSuperAppRateLimits();
        services.AddOpenApi();
        services.AddHealthChecks()
            .AddSqlServer(configuration.GetConnectionString("Default")!, name: "database", tags: ["ready"]);
        return services;
    }
}
