using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Agents;
using SuperApp.Application.Features.CeoSystem;

namespace SuperApp.Infrastructure.Admin;

/// <summary>
/// Registers the Phase 7 infrastructure (agents and keys, switches, org chart, integrations, admin readers) — one call
/// from <c>DependencyInjection.AddInfrastructure</c>. The audit logger is replaced by the agent-aware one.
/// </summary>
public static class AdminInfrastructure
{
    public static IServiceCollection AddAdminInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IAgentKeyStore, AgentKeyStore>();
        services.AddScoped<ISwitchRepository, SwitchRepository>();
        services.AddScoped<IOrgRepository, OrgRepository>();
        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddSingleton<ISecretPresence, EnvSecretPresence>();
        services.AddScoped<IAiPanelReader, AiPanelReader>();
        services.AddScoped<AdminReader>();
        services.AddScoped<Application.Features.AdminConsole.IAdminReader>(sp => sp.GetRequiredService<AdminReader>());
        services.AddScoped<Application.Features.AdminConsole.IAdminPeopleRepository, AdminPeopleRepository>();
        services.AddScoped<Application.Features.AdminConsole.IPlatformSettingsRepository, PlatformSettingsRepository>();
        services.AddScoped<Application.Features.AdminConsole.IBroadcastRepository, BroadcastRepository>();
        services.AddScoped<Application.Features.AdminConsole.IReportsReader, ReportsReader>();
        services.AddHostedService<BroadcastScheduler>();
        services.Replace(ServiceDescriptor.Scoped<IAuditLogger, AgentAwareAuditLogger>());
        return services;
    }
}
