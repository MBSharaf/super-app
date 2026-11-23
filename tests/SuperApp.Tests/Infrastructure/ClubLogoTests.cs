using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using SuperApp.Infrastructure.Football;

namespace SuperApp.Tests.Infrastructure;

/// <summary>Owner's rule (2026-11-23): matches and fantasy show the real club badges the data sources publish.</summary>
public sealed class ClubLogoTests
{
    private sealed class Stub(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
    }

    [Fact]
    public async Task TheSportsDb_clubs_carry_their_badge()
    {
        const string json = """
            {"events":[{"idEvent":"1","idHomeTeam":"139845","idAwayTeam":"138997","strHomeTeam":"El Gouna","strAwayTeam":"Zamalek",
              "strHomeTeamBadge":"https://r2.thesportsdb.com/images/media/team/badge/a.png","strAwayTeamBadge":null,
              "strTimestamp":"2026-11-23T15:00:00","strStatus":"Not Started"}]}
            """;
        var data = new TheSportsDbFootballData(new HttpClient(new Stub(json)) { BaseAddress = new Uri(TheSportsDbFootballData.BaseAddress + "123/") },
            NullLogger<TheSportsDbFootballData>.Instance);
        await data.FixturesAsync(4829, 2026, [9], CancellationToken.None);
        var clubs = await data.ClubsAsync(4829, 2026, CancellationToken.None);
        clubs.Single(c => c.ExternalId == 139845).LogoUrl.ShouldBe("https://r2.thesportsdb.com/images/media/team/badge/a.png/small");
        clubs.Single(c => c.ExternalId == 138997).LogoUrl.ShouldBeNull();
    }

    [Fact]
    public async Task Premier_League_clubs_carry_their_badge()
    {
        const string json = """{"events":[],"teams":[{"id":1,"code":3,"name":"Arsenal","short_name":"ARS"}]}""";
        var data = new FplFootballData(new HttpClient(new Stub(json)) { BaseAddress = new Uri(FplFootballData.BaseAddress) });
        var club = (await data.ClubsAsync(8, 2026, CancellationToken.None)).Single();
        club.LogoUrl.ShouldBe("https://resources.premierleague.com/premierleague/badges/t3.svg");
    }
}
