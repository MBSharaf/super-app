using System.Text.Json;
using Shouldly;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Football;

namespace SuperApp.Tests.Infrastructure;

/// <summary>
/// Owner's report (2026-11-24): Zamalek's CAF Champions League match on Nov 28 and the whole CAF Champions League were
/// missing. Leagues and cups now read the whole season from FotMob's league page.
/// </summary>
public sealed class FotMobFixturesTests
{
    private const string League = """
        {"details":{"selectedSeason":"2026/2027"},
         "fixtures":{"allMatches":[
          {"round":"1","roundName":1,"id":"4900001","home":{"name":"Zamalek SC","id":"80591"},"away":{"name":"Club Africain","id":"6339"},
           "status":{"utcTime":"2026-11-28T16:00:00Z","started":false,"finished":false,"cancelled":false}},
          {"round":"Group Stage - Round 2","roundName":"Group Stage - Round 2","id":"4900002","home":{"name":"Pyramids FC","id":"101918"},"away":{"name":"MAS Fès","id":"6339"},
           "status":{"utcTime":"2026-12-05T16:00:00Z","started":true,"finished":true,"cancelled":false,"scoreStr":"2 - 1","reason":{"short":"FT"}}},
          {"round":"1/4","roundName":"Quarter-Finals","id":"4900003","home":{"name":"Al Ahly SC","id":"101919"},"away":{"name":"Espérance","id":"6340"},
           "status":{"utcTime":"2027-03-14T19:00:00Z","started":true,"finished":false,"cancelled":false,"scoreStr":"0 - 0"}},
          {"round":"final","roundName":"Final","id":"4900004","home":{"name":"Winner SF 1","id":"1"},"away":{"name":"Winner SF 2","id":"2"},
           "status":{"utcTime":"2027-05-20T19:00:00Z","started":false,"finished":false,"cancelled":false}},
          {"round":"1","roundName":1,"id":"4900005","home":{"name":"TBC","id":"80592"},"away":{"name":"Club Africain","id":"6339"},
           "status":{"utcTime":"2026-11-29T16:00:00Z","started":false,"finished":false,"cancelled":false}}
         ]}}
        """;

    [Fact]
    public void A_whole_season_is_read_with_negative_ids_and_the_sites_round_codes()
    {
        using var doc = JsonDocument.Parse(League);
        var (season, fixtures, clubs) = FotMobFootballData.Parse(doc.RootElement);

        season.ShouldBe(2026);
        fixtures.Count.ShouldBe(3);   // the final waits for the draw; "TBC" is no club
        var zamalek = fixtures.Single(f => f.ExternalId == -4900001);
        (zamalek.HomeClubExternalId, zamalek.AwayClubExternalId, zamalek.Round, zamalek.Status).ShouldBe((-80591, -6339, 1, FixtureStatus.Scheduled));
        zamalek.KickoffAt.ShouldBe(new DateTime(2026, 11, 28, 16, 0, 0, DateTimeKind.Utc));
        var played = fixtures.Single(f => f.ExternalId == -4900002);
        (played.Round, played.Status, played.HomeGoals, played.AwayGoals).ShouldBe((2, FixtureStatus.Finished, 2, 1));
        var live = fixtures.Single(f => f.ExternalId == -4900003);
        (live.Round, live.Status, live.HomeGoals).ShouldBe((125, FixtureStatus.Live, 0));

        clubs[-80591].Name.ShouldBe("Zamalek SC");
        clubs[-80591].LogoUrl.ShouldBe("https://images.fotmob.com/image_resources/logo/teamlogo/80591_small.png");
        clubs.ContainsKey(-1).ShouldBeFalse();
    }

    [Theory]
    [InlineData("14", null, 14)]
    [InlineData("Group Stage - Round 3", null, 3)]
    [InlineData("1st Preliminary Round", "1", 101)]
    [InlineData("2nd Preliminary Round", "2", 102)]
    [InlineData("playoff", "playoff", 32)]
    [InlineData("1/16", "Round of 32", 33)]
    [InlineData("1/8", "Round of 16", 16)]
    [InlineData("1/2", "Semi-Finals", 150)]
    [InlineData("bronze", "Bronze Final", 190)]
    [InlineData("final", "Final", 200)]
    [InlineData("something new", null, null)]
    public void Rounds_get_the_sites_codes(string round, string? name, int? code) => FotMobFootballData.RoundCode(round, name).ShouldBe(code);

    [Fact]
    public void A_league_page_without_a_season_is_refused() =>
        Should.Throw<HttpRequestException>(() => FotMobFootballData.Parse(JsonDocument.Parse("""{"details":{}}""").RootElement));
}
