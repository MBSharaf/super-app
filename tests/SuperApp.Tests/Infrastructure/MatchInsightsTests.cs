using System.Text.Json;
using Shouldly;
using SuperApp.Infrastructure.Football;

namespace SuperApp.Tests.Infrastructure;

/// <summary>Owner's ask (2026-11-24): a table for every competition and match statistics like the big score sites.</summary>
public sealed class MatchInsightsTests
{
    [Fact]
    public void A_league_table_reads_with_its_legend()
    {
        const string json = """
            {"table":[{"data":{"selectedSeason":"2026/2027","legend":[{"title":"CAF Champions League","color":"#2AD572","indices":[0]}],
              "table":{"all":[
                {"name":"Pyramids FC","id":517894,"played":9,"wins":9,"draws":0,"losses":0,"scoresStr":"21-4","goalConDiff":17,"pts":27,"idx":1,"qualColor":"#2AD572"},
                {"name":"Al Ahly SC","id":101745,"played":9,"wins":6,"draws":1,"losses":2,"scoresStr":"15-7","goalConDiff":8,"pts":19,"idx":2,"qualColor":null}]}}}]}
            """;
        using var doc = JsonDocument.Parse(json);
        var table = FotMobParse.Standings("egy", doc.RootElement);
        table.Season.ShouldBe("2026/2027");
        var rows = table.Groups.Single().Rows;
        rows[0].ShouldBe(new(1, "Pyramids FC", "https://images.fotmob.com/image_resources/logo/teamlogo/517894_small.png", 9, 9, 0, 0, 21, 4, 17, 27, "#2AD572"));
        (rows[1].GoalsFor, rows[1].GoalsAgainst, rows[1].Points).ShouldBe((15, 7, 19));
        table.Legend.Single().Title.ShouldBe("CAF Champions League");
    }

    [Fact]
    public void A_cup_table_reads_group_by_group()
    {
        const string json = """
            {"table":[{"data":{"tables":[
              {"leagueName":"Group A","table":{"all":[{"name":"Zamalek","id":1,"played":2,"wins":2,"draws":0,"losses":0,"scoresStr":"3-0","goalConDiff":3,"pts":6,"idx":1}]}},
              {"leagueName":"Group B","table":{"all":[{"name":"Pyramids FC","id":2,"played":2,"wins":1,"draws":1,"losses":0,"scoresStr":"2-1","goalConDiff":1,"pts":4,"idx":1}]}}]}}]}
            """;
        using var doc = JsonDocument.Parse(json);
        var groups = FotMobParse.Standings("cafcl", doc.RootElement).Groups;
        groups.Select(g => g.Name).ShouldBe(["Group A", "Group B"]);
        groups[1].Rows.Single().Club.ShouldBe("Pyramids FC");
    }

    [Fact]
    public void Match_facts_read_stats_events_and_lineups()
    {
        const string json = """
            {"content":{
              "stats":{"Periods":{"All":{"stats":[{"title":"Top stats","stats":[
                {"title":"Top stats","key":"t","stats":[null,null],"type":"title"},
                {"title":"Ball possession","key":"BallPossesion","stats":[43,57],"type":"graph"},
                {"title":"Expected goals (xG)","key":"expected_goals","stats":["1.04","1.19"],"type":"text"},
                {"title":"Accurate passes","key":"accurate_passes","stats":["245 (83%)","301 (86%)"],"type":"text"}]}]}}},
              "matchFacts":{"events":{"events":[
                {"type":"Card","time":23,"overloadTime":null,"isHome":true,"player":{"name":"Marwan Attia"},"card":"Yellow"},
                {"type":"Goal","time":32,"isHome":false,"player":{"name":"Karim Hafez"},"homeScore":0,"awayScore":1,"ownGoal":null,"goalDescriptionKey":"penalty","suffix":"Pen"},
                {"type":"Half","time":45},
                {"type":"Substitution","time":46,"overloadTime":0,"isHome":true,"swap":[{"name":"Taher Mohamed"},{"name":"Achraf Bencharki"}]},
                {"type":"Goal","time":90,"overloadTime":3,"isHome":false,"player":{"name":"Fiston Mayele"},"homeScore":0,"awayScore":2}]}},
              "lineup":{"homeTeam":{"formation":"4-3-3","coach":{"name":"Houssaine Ammouta"},
                "starters":[{"name":"Mostafa Shobeir","shirtNumber":"31","performance":{"rating":7}}],"subs":[{"name":"Taher Mohamed","shirtNumber":"10"}]},
                "awayTeam":{"starters":[]}}}}
            """;
        using var doc = JsonDocument.Parse(json);
        var facts = FotMobParse.Facts(doc.RootElement);
        var items = facts.Stats.Single().Items;
        items.Select(i => i.Title).ShouldBe(["Ball possession", "Expected goals (xG)", "Accurate passes"]);
        (items[0].Home, items[0].Away, items[0].Bar).ShouldBe(("43", "57", true));
        items[2].Home.ShouldBe("245 (83%)");
        FotMobParse.Lead("245 (83%)").ShouldBe(245);

        facts.Events.Count.ShouldBe(4);
        facts.Events[0].ShouldBe(new(23, null, "card", "home", "Marwan Attia", "Yellow", null, null, null, null));
        facts.Events[1].Detail.ShouldBe("Penalty");
        (facts.Events[2].PlayerIn, facts.Events[2].PlayerOut).ShouldBe(("Taher Mohamed", "Achraf Bencharki"));
        (facts.Events[3].Minute, facts.Events[3].Added, facts.Events[3].AwayScore).ShouldBe((90, 3, 2));

        facts.Home!.Formation.ShouldBe("4-3-3");
        facts.Home.Coach.ShouldBe("Houssaine Ammouta");
        facts.Home.Starters.Single().ShouldBe(new("Mostafa Shobeir", "31", "7"));
        facts.Away.ShouldBeNull();
    }
}
