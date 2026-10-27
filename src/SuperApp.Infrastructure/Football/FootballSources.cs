using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SuperApp.Application.Features.Football;

namespace SuperApp.Infrastructure.Football;

/// <summary>
/// Picks each competition's provider from its <c>Provider</c> column: "fpl", "thesportsdb", or "api-football" (only when
/// <c>FOOTBALL__APIFOOTBALL__KEY</c> is set, else the simulated data). With <c>Football:Offline</c> (the tests) nothing goes
/// out: the simulated provider answers for the leagues it knows, the others are skipped.
/// </summary>
public sealed class FootballSources(IServiceProvider services, IConfiguration configuration) : IFootballSources
{
    public IFootballData? For(CompetitionRef competition)
    {
        if (configuration.GetValue("Football:Offline", false))
        {
            return SimulatedLeagues.Find(competition.ExternalLeagueId) is null ? null : services.GetRequiredService<SimulatedFootballData>();
        }
        return competition.Provider switch
        {
            "fpl" => services.GetRequiredService<FplFootballData>(),
            "thesportsdb" => services.GetRequiredService<TheSportsDbFootballData>(),
            "api-football" when !string.IsNullOrWhiteSpace(configuration["Football:ApiFootball:Key"]) => services.GetRequiredService<ApiFootballClient>(),
            "api-football" => services.GetRequiredService<SimulatedFootballData>(),
            _ => null,
        };
    }
}
