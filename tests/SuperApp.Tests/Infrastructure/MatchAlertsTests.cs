using Shouldly;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Football;

namespace SuperApp.Tests.Infrastructure;

/// <summary>Which match moments are told, and how they read in Arabic and English.</summary>
public sealed class MatchAlertsTests
{
    private static MatchState S(FixtureStatus s, int? h = null, int? a = null) => new(s, h, a);

    [Fact]
    public void Kick_off_goals_and_full_time_are_moments_and_a_disallowed_goal_is_not()
    {
        MatchAlerts.Moments(S(FixtureStatus.Scheduled), S(FixtureStatus.Live, 0, 0)).ShouldBe([MatchMoment.Kickoff]);
        MatchAlerts.Moments(S(FixtureStatus.Live, 0, 0), S(FixtureStatus.Live, 1, 0)).ShouldBe([MatchMoment.Goal]);
        MatchAlerts.Moments(S(FixtureStatus.Live, 1, 0), S(FixtureStatus.Live, 0, 0)).ShouldBeEmpty();
        MatchAlerts.Moments(S(FixtureStatus.Live, 1, 0), S(FixtureStatus.Finished, 1, 1)).ShouldBe([MatchMoment.Goal, MatchMoment.FullTime]);
        MatchAlerts.Moments(S(FixtureStatus.Scheduled), S(FixtureStatus.Finished, 2, 0)).ShouldBe([MatchMoment.Goal, MatchMoment.FullTime]);
        MatchAlerts.Moments(S(FixtureStatus.Finished, 2, 0), S(FixtureStatus.Finished, 2, 0)).ShouldBeEmpty();
    }

    [Fact]
    public void Messages_use_the_clubs_name_in_the_persons_language_and_link_to_the_match_centre()
    {
        var kickoff = new DateTime(2026, 11, 24, 22, 30, 0, DateTimeKind.Utc);   // 00:30 on the 25th in Cairo
        var alert = new MatchAlert(42, kickoff, new AlertClub(1, "Al Ahly", "الأهلي"), new AlertClub(2, "Pyramids", ""), MatchMoment.Goal, 0, 1, 67);
        var ar = MatchAlerts.Message(alert, "ar");
        ar.Title.ShouldBe("⚽ جووول Pyramids!");
        ar.Body.ShouldBe("الأهلي 0–1 Pyramids · الدقيقة 67");
        ar.Link.ShouldBe("#/matches?date=2026-11-25&open=m-42");
        MatchAlerts.Message(alert with { Moment = MatchMoment.FullTime }, "en").Title.ShouldBe("Full time: Al Ahly 0–1 Pyramids");
        MatchAlerts.Message(alert with { Moment = MatchMoment.Lineups }, "ar").Title.ShouldBe("التشكيلة نزلت: الأهلي ضد Pyramids");
    }
}
