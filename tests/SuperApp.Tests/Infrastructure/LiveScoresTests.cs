using System.Text.Json;
using Shouldly;
using SuperApp.Application.Features.Football;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Football;
using SuperApp.Infrastructure.Persistence.Repositories;

namespace SuperApp.Tests.Infrastructure;

/// <summary>Owner's report (2026-11-23): live matches showed a made-up minute and 0-0 while Pyramids led Al Ahly 1-0 at half-time.</summary>
public sealed class LiveScoresTests
{
    private const string Feed = """
        {"leagues":[{"ccode":"EGY","primaryId":519,"name":"Premier League","matches":[
          {"id":5996763,"home":{"id":101745,"score":0,"name":"Al Ahly SC"},"away":{"id":517894,"score":1,"name":"Pyramids FC"},
           "status":{"utcTime":"2026-11-23T18:00:00.000Z","finished":false,"started":true,"cancelled":false,"ongoing":true,"scoreStr":"0 - 1",
             "liveTime":{"short":"HT","long":"Half-Time","maxTime":45,"addedTime":0}}},
          {"id":5996762,"home":{"id":178268,"score":1,"name":"El Gouna FC"},"away":{"id":80591,"score":2,"name":"Zamalek SC"},
           "status":{"utcTime":"2026-11-23T15:00:00.000Z","finished":true,"started":true,"cancelled":false,"reason":{"short":"FT"}}},
          {"id":5996764,"home":{"id":1,"score":0,"name":"Wadi Degla FC"},"away":{"id":2,"score":0,"name":"Al Ittihad Alexandria"},
           "status":{"utcTime":"2026-11-23T20:00:00.000Z","finished":false,"started":false,"cancelled":false}}]}]}
        """;

    private static IReadOnlyList<LiveMatch> Parsed() { using var doc = JsonDocument.Parse(Feed); return FotMobLiveFeed.Parse(doc.RootElement); }

    [Fact]
    public void Feed_gives_score_status_and_clock()
    {
        var list = Parsed();
        var ahly = list.Single(m => m.Id == 5996763);
        (ahly.Status, ahly.HomeGoals, ahly.AwayGoals, ahly.Clock, ahly.Minute).ShouldBe((FixtureStatus.Live, 0, 1, "HT", 45));
        ahly.KickoffUtc.ShouldBe(new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc));
        list.Single(m => m.Id == 5996762).Status.ShouldBe(FixtureStatus.Finished);
        var later = list.Single(m => m.Id == 5996764);
        (later.Status, later.HomeGoals).ShouldBe((null, null));
    }

    [Theory]
    [InlineData("57'", 57)]
    [InlineData("45+2'", 45)]
    [InlineData("HT", 45)]
    [InlineData(null, null)]
    public void Minute_comes_from_the_clock(string? clock, int? minute) => FotMobLiveFeed.MinuteOf(clock).ShouldBe(minute);

    [Fact]
    public void Our_fixtures_pair_with_the_feed_by_names_and_kickoff()
    {
        var ko = new DateTime(2026, 11, 23, 18, 0, 0, DateTimeKind.Utc);
        var ours = new List<LiveMatcher.Ours>
        {
            new(1, "Al Ahly", "Pyramids", ko),
            new(2, "El Gouna", "Zamalek", ko.AddHours(-3)),
            new(3, "Ittihad Alexandria", "Wadi Degla", ko.AddHours(2)),   // home and away swapped: not paired
            new(4, "Arsenal", "Chelsea", ko),
        };
        var pairs = LiveMatcher.Pair(ours, Parsed());
        pairs[1].Id.ShouldBe(5996763);
        pairs[2].Id.ShouldBe(5996762);
        pairs.ContainsKey(3).ShouldBeFalse();
        pairs.ContainsKey(4).ShouldBeFalse();
    }

    [Fact]
    public void A_late_source_does_not_undo_the_live_score()
    {
        var row = new Fixture { Status = FixtureStatus.Live, HomeGoals = 0, AwayGoals = 1 };
        FantasySyncRepository.BehindLive(row, Fd(FixtureStatus.Scheduled, null, null)).ShouldBeTrue();
        FantasySyncRepository.BehindLive(row, Fd(FixtureStatus.Live, 0, 0)).ShouldBeTrue();
        FantasySyncRepository.BehindLive(row, Fd(FixtureStatus.Finished, 0, 1)).ShouldBeFalse();
        FantasySyncRepository.BehindLive(row, Fd(FixtureStatus.Postponed, null, null)).ShouldBeFalse();
        FantasySyncRepository.BehindLive(new Fixture { Status = FixtureStatus.Scheduled }, Fd(FixtureStatus.Live, 1, 0)).ShouldBeFalse();
    }

    [Fact]
    public void The_fixture_sync_leaves_a_match_on_now_to_the_live_job()
    {
        var now = new DateTime(2026, 11, 24, 21, 9, 0, DateTimeKind.Utc);
        var playing = new Fixture { Status = FixtureStatus.Live, KickoffAt = now.AddMinutes(-69), HomeGoals = 0, AwayGoals = 1 };
        FantasySyncRepository.LiveOwns(playing, Fd(FixtureStatus.Live, 1, 1), now).ShouldBeTrue();   // the goal alert stays with the live job
        FantasySyncRepository.LiveOwns(playing, Fd(FixtureStatus.Finished, 1, 1), now).ShouldBeTrue();
        FantasySyncRepository.LiveOwns(playing, Fd(FixtureStatus.Postponed, null, null), now).ShouldBeFalse();
        FantasySyncRepository.LiveOwns(new Fixture { Status = FixtureStatus.Scheduled, KickoffAt = now.AddMinutes(30) }, Fd(FixtureStatus.Live, 0, 0), now).ShouldBeTrue();
        FantasySyncRepository.LiveOwns(new Fixture { Status = FixtureStatus.Live, KickoffAt = now.AddHours(-4) }, Fd(FixtureStatus.Finished, 2, 1), now).ShouldBeFalse();
        FantasySyncRepository.LiveOwns(new Fixture { Status = FixtureStatus.Finished, KickoffAt = now.AddMinutes(-100) }, Fd(FixtureStatus.Finished, 2, 1), now).ShouldBeFalse();
        FantasySyncRepository.LiveOwns(new Fixture { Status = FixtureStatus.Scheduled, KickoffAt = now.AddDays(2) }, Fd(FixtureStatus.Scheduled, null, null), now).ShouldBeFalse();
    }

    [Fact]
    public void The_live_clock_is_dropped_when_stale()
    {
        var clocks = new LiveClock();
        var now = new DateTime(2026, 11, 23, 19, 0, 0, DateTimeKind.Utc);
        clocks.Set(7, "HT", now);
        clocks.Get(7, now.AddMinutes(1)).ShouldBe("HT");
        clocks.Get(7, now.AddMinutes(10)).ShouldBeNull();
    }

    private static FdFixture Fd(FixtureStatus status, int? hs, int? aws) => new(1, 1, 1, 2, DateTime.UtcNow, status, hs, aws, null, null);
}
