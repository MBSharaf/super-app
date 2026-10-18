using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Fantasy;
using SuperApp.Application.Features.Football;
using SuperApp.Infrastructure.Football;
using SuperApp.Infrastructure.Persistence.Repositories;

namespace SuperApp.Infrastructure.Fantasy;

/// <summary>
/// Registers the Phase 6 infrastructure (football provider, quota, repositories, the sync schedule) — one call from
/// <c>DependencyInjection.AddInfrastructure</c>. API-FOOTBALL is used only when <c>FOOTBALL__APIFOOTBALL__KEY</c> is set;
/// otherwise the simulated provider answers.
/// </summary>
public static class FantasyInfrastructureRegistration
{
    public static IServiceCollection AddFantasyInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var key = configuration["Football:ApiFootball:Key"];
        services.AddSingleton<FootballUsageRecorder>();
        if (string.IsNullOrWhiteSpace(key))
        {
            services.AddScoped<IFootballData, SimulatedFootballData>();
        }
        else
        {
            services.AddHttpClient<IFootballData, ApiFootballClient>(http =>
            {
                http.BaseAddress = new Uri(ApiFootballClient.BaseAddress);
                http.DefaultRequestHeaders.Add("x-apisports-key", key);
                http.Timeout = TimeSpan.FromSeconds(20);
            });
        }
        services.AddScoped<IFootballQuota, FootballQuota>();
        services.AddScoped<IFantasySyncRepository, FantasySyncRepository>();
        services.AddScoped<IFantasyScoringRepository, FantasyScoringRepository>();
        services.AddScoped<IFantasyReader, FantasyReader>();
        services.AddScoped<IFantasyTeamRepository, FantasyTeamRepository>();
        services.AddScoped<IFantasyGroupRepository, FantasyGroupRepository>();
        services.AddScoped<IFantasyPassRepository, FantasyPassRepository>();
        services.AddScoped<ISponsorRepository, SponsorRepository>();
        services.AddScoped<IPrizeVouchers, PrizeVouchers>();
        services.AddScoped<Application.Features.Content.IMatchesReader, MatchesReader>();
        services.AddScoped<Application.Features.Content.INewsRepository, NewsRepository>();
        services.AddScoped<Application.Features.Content.IContentRepository, ContentRepository>();
        services.AddScoped<Application.Features.Content.ILegalHelpRepository, LegalHelpRepository>();
        services.AddScoped<Application.Features.Communities.ICommunityRepository, CommunityRepository>();
        services.AddHostedService<FootballScheduler>();
        return services;
    }
}
