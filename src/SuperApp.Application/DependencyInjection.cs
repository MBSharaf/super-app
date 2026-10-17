using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Auth.Services;

namespace SuperApp.Application;

/// <summary>Registers the Application layer: business services and the request validators.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMeService, MeService>();
        return services;
    }
}
