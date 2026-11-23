using System.Net;
using System.Text;
using Shouldly;
using SuperApp.Infrastructure.Football;

namespace SuperApp.Tests.Infrastructure;

/// <summary>
/// Owner's ask (2026-11-23): fantasy follows the official game — photos, prices, news, goals conceded on the pitch,
/// defensive contribution and the official bonus are read from the FPL files (shapes as served on 2026-11-23).
/// </summary>
public sealed class FplReaderTests
{
    private sealed class Files(Dictionary<string, string> byPath) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(byPath[request.RequestUri!.AbsolutePath.Replace("/api/", "")], Encoding.UTF8, "application/json"),
            });
    }

    private const string Bootstrap = """
        {"events":[{"deadline_time":"2026-08-15T17:30:00Z"}],"teams":[{"id":5,"code":36,"name":"Brighton","short_name":"BHA"},{"id":15,"code":43,"name":"Man City","short_name":"MCI"}],
         "elements":[
          {"id":10,"team":5,"web_name":"De Cuyper","element_type":2,"now_cost":55,"status":"a","code":465730,"news":"","chance_of_playing_next_round":null},
          {"id":20,"team":15,"web_name":"Haaland","element_type":4,"now_cost":158,"status":"d","code":223094,"news":"Knock - 75% chance of playing","chance_of_playing_next_round":75}]}
        """;

    private const string Fixtures = """
        [{"id":109,"event":11,"team_h":5,"team_a":15,"kickoff_time":"2026-11-21T15:00:00Z","finished_provisional":true,"started":true,"team_h_score":1,"team_a_score":2,"minutes":90,
          "stats":[{"identifier":"goals_scored","h":[],"a":[{"value":2,"element":20}]},{"identifier":"bonus","h":[],"a":[{"value":3,"element":20}]},
                   {"identifier":"bps","h":[{"value":30,"element":10}],"a":[{"value":50,"element":20}]},{"identifier":"defensive_contribution","h":[{"value":11,"element":10}],"a":[]}]}]
        """;

    private const string Live = """
        {"elements":[
          {"id":10,"stats":{"minutes":90,"goals_conceded":2,"clean_sheets":0},"explain":[{"fixture":109,"stats":[{"identifier":"minutes","points":2,"value":90},{"identifier":"goals_conceded","points":-1,"value":2},{"identifier":"defensive_contribution","points":2,"value":11}]}]},
          {"id":20,"stats":{"minutes":80,"goals_conceded":1,"clean_sheets":0},"explain":[{"fixture":109,"stats":[{"identifier":"minutes","points":2,"value":80}]}]}]}
        """;

    private static FplFootballData Reader() => new(new HttpClient(new Files(new()
    {
        ["bootstrap-static/"] = Bootstrap, ["fixtures/"] = Fixtures, ["event/11/live/"] = Live,
    })) { BaseAddress = new Uri(FplFootballData.BaseAddress) });

    [Fact]
    public async Task Players_carry_the_official_price_photo_and_news()
    {
        var haaland = (await Reader().SquadAsync(8, 2026, 15, CancellationToken.None)).Single();
        haaland.Price.ShouldBe(15.8m);
        haaland.PhotoUrl.ShouldBe("https://resources.premierleague.com/premierleague25/photos/players/110x140/223094.png");
        haaland.News.ShouldBe("Knock - 75% chance of playing");
        haaland.ChanceOfPlaying.ShouldBe(75);
    }

    [Fact]
    public async Task Match_numbers_include_goals_conceded_on_the_pitch_defensive_contribution_and_the_official_bonus()
    {
        var stats = await Reader().FixtureStatsAsync(8, 109, CancellationToken.None);
        var defender = stats.Single(s => s.PlayerExternalId == 10);
        (defender.Minutes, defender.GoalsConceded, defender.CleanSheet, defender.DefensiveContribution, defender.Bonus).ShouldBe((90, 2, false, 11, 0));
        var striker = stats.Single(s => s.PlayerExternalId == 20);
        (striker.Goals, striker.Bonus, striker.GoalsConceded).ShouldBe((2, 3, 1));
    }
}
