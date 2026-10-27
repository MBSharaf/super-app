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
        services.AddScoped<Football.ICompetitionsAdminService, Football.CompetitionsAdminService>();
        services.AddScoped<IFantasyGroupsService, FantasyGroupsService>();
        services.AddScoped<IFantasyPassService, FantasyPassService>();
        services.AddScoped<IFantasyLeaguesAdminService, FantasyLeaguesAdminService>();
        services.AddScoped<Content.INewsService, Content.NewsService>();
        services.AddScoped<Content.IContentService, Content.ContentService>();
        services.AddScoped<Content.IContentAdminService, Content.ContentAdminService>();
        services.AddScoped<Content.ILegalHelpService, Content.LegalHelpService>();
        services.AddScoped<Communities.ICommunitiesService, Communities.CommunitiesService>();
        services.AddScoped<Communities.IPostsService, Communities.PostsService>();
        services.AddScoped<Communities.ICommunityAdminService, Communities.CommunityAdminService>();
        services.AddScoped<Support.ISupportService, Support.SupportService>();
        services.AddScoped<Support.ISupportAdminService, Support.SupportAdminService>();
        return services;
    }
}
