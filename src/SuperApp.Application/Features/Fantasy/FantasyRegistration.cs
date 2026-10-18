using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Football;

namespace SuperApp.Application.Features.Fantasy;

/// <summary>
/// Registers the Phase 6 services (fantasy, football sync and scoring, groups, content, communities, support) — one call
/// from <c>DependencyInjection.AddApplication</c>, so parallel branches never touch the same lines. Validators are
/// found by the assembly scan.
/// </summary>
public static class FantasyRegistration
{
    public static IServiceCollection AddFantasy(this IServiceCollection services)
    {
        services.AddScoped<IFootballSyncService, FootballSyncService>();
        services.AddScoped<IFantasyScoringService, FantasyScoringService>();
        services.AddScoped<IFantasyService, FantasyService>();
        services.AddScoped<IFantasyTeamService, FantasyTeamService>();
        services.AddScoped<IFantasyAdminService, FantasyAdminService>();
        services.AddScoped<IFantasyGroupsService, FantasyGroupsService>();
        services.AddScoped<IFantasyPassService, FantasyPassService>();
        services.AddScoped<IFantasyLeaguesAdminService, FantasyLeaguesAdminService>();
        services.AddScoped<Content.INewsService, Content.NewsService>();
        return services;
    }
}
