using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Agents;
using SuperApp.Application.Features.CeoSystem;

namespace SuperApp.Application.Features.AdminConsole;

/// <summary>
/// Registers the Phase 7 services (AI agents and their keys, CEO controls, org chart, integrations, the admin console)
/// — one call from <c>DependencyInjection.AddApplication</c>. Validators are found by the assembly scan.
/// </summary>
public static class AdminRegistration
{
    public static IServiceCollection AddAdmin(this IServiceCollection services)
    {
        services.AddScoped<IAgentsService, AgentsService>();
        services.AddScoped<IAgentSelfService, AgentSelfService>();
        services.AddScoped<ICeoControlService, CeoControlService>();
        services.AddScoped<IOrgService, OrgService>();
        services.AddScoped<IIntegrationsService, IntegrationsService>();
        services.AddScoped<IAdminPeopleService, AdminPeopleService>();
        services.AddScoped<IAdminOpsService, AdminOpsService>();
        services.AddScoped<IPlatformService, PlatformService>();
        return services;
    }
}
