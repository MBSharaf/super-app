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
        services.AddScoped<Application.Features.Support.ISupportRepository, SupportRepository>();
        RegisterEmails();
        services.AddHostedService<FootballScheduler>();
        return services;
    }

    /// <summary>Support emails in Arabic and English: the auto-reply when a ticket opens, and the team's replies.</summary>
    private static void RegisterEmails()
    {
        Email.EmailTemplates.Register("ticket-opened", "en", "We got your message ({code}) — Super App",
            ["Hi {name},", "Thanks for contacting Super App about \"{subject}\".", "Our team replies within 24 hours. You can add details or photos in the chat of ticket {code}."], null);
        Email.EmailTemplates.Register("ticket-opened", "ar", "وصلتنا رسالتك ({code}) — Super App",
            ["أهلاً {name}،", "شكراً إنك كلمت Super App بخصوص \"{subject}\".", "فريقنا هيرد خلال 24 ساعة. تقدر تضيف تفاصيل أو صور في محادثة التذكرة {code}."], null);
        Email.EmailTemplates.Register("ticket-reply", "en", "{title} — Super App",
            ["Hi {name},", "{body}", "Open ticket {code} in Super App to reply."], null);
        Email.EmailTemplates.Register("ticket-reply", "ar", "رد على تذكرتك {code} — Super App",
            ["أهلاً {name}،", "{body}", "افتح التذكرة {code} في Super App عشان ترد."], null);
    }
}
