using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Football;

/// <summary>What a follower of a club is told about its match.</summary>
public enum MatchMoment
{
    Lineups,
    Kickoff,
    Goal,
    FullTime,
}

/// <summary>A match's state before and after a live update.</summary>
public sealed record MatchState(FixtureStatus Status, int? HomeGoals, int? AwayGoals);

/// <summary>A club as the alert names it (Arabic name when the person reads Arabic and the club has one).</summary>
public sealed record AlertClub(long Id, string NameEn, string NameAr)
{
    public string Name(string lang) => lang != "ar" ? NameEn : !string.IsNullOrWhiteSpace(NameAr) ? NameAr : ClubNamesAr.Of(NameEn) ?? NameEn;
}

/// <summary>A match alert to send: the fixture, its clubs, the moment and the score at that moment.</summary>
public sealed record MatchAlert(long FixtureId, DateTime KickoffAt, AlertClub Home, AlertClub Away, MatchMoment Moment, int? HomeGoals, int? AwayGoals,
    int? Minute, bool HomeScored = false);

/// <summary>
/// Match alerts for the clubs people follow: line-ups, kick-off, every goal and the final whistle. The live job finds the
/// moments (by comparing the match before and after each update); the alert is written in the person's language.
/// </summary>
public static class MatchAlerts
{
    private static readonly TimeZoneInfo Cairo = TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo");

    /// <summary>The moments between two states of a match (a goal taken back by VAR is not a moment).</summary>
    public static IReadOnlyList<MatchMoment> Moments(MatchState before, MatchState after)
    {
        var list = new List<MatchMoment>();
        if (before.Status == FixtureStatus.Scheduled && after.Status == FixtureStatus.Live)
        {
            list.Add(MatchMoment.Kickoff);
        }
        if (after.Status is FixtureStatus.Live or FixtureStatus.Finished
            && (after.HomeGoals ?? 0) + (after.AwayGoals ?? 0) > (before.HomeGoals ?? 0) + (before.AwayGoals ?? 0))
        {
            list.Add(MatchMoment.Goal);
        }
        if (before.Status != FixtureStatus.Finished && after.Status == FixtureStatus.Finished)
        {
            list.Add(MatchMoment.FullTime);
        }
        return list;
    }

    /// <summary>The match centre on the matches page (Cairo day of the kick-off).</summary>
    public static string Link(long fixtureId, DateTime kickoffUtc) =>
        $"#/matches?date={TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(kickoffUtc, DateTimeKind.Utc), Cairo):yyyy-MM-dd}&open=m-{fixtureId}";

    public static NotificationMessage Message(MatchAlert a, string lang)
    {
        var ar = lang == "ar";
        var (home, away) = (a.Home.Name(lang), a.Away.Name(lang));
        var score = string.Create(CultureInfo.InvariantCulture, $"{home} {a.HomeGoals ?? 0}–{a.AwayGoals ?? 0} {away}");
        var minute = a.Minute is { } m ? (ar ? $"الدقيقة {m}" : $"{m}'") : null;
        var (title, body, tone) = a.Moment switch
        {
            MatchMoment.Lineups => (ar ? $"التشكيلة نزلت: {home} ضد {away}" : $"Line-ups are out: {home} vs {away}",
                ar ? "شوف التشكيلة الأساسية والبدلاء." : "See the starting elevens and the bench.", NotificationTone.Info),
            MatchMoment.Kickoff => (ar ? $"الماتش بدأ: {home} ضد {away}" : $"Kick-off: {home} vs {away}",
                ar ? "تابعه لايف على سوبر ملعب." : "Follow it live on Super Malaab.", NotificationTone.Info),
            MatchMoment.Goal => (ar ? $"⚽ جووول {(a.HomeScored ? home : away)}!" : $"⚽ Goal for {(a.HomeScored ? home : away)}!",
                minute is null ? score : $"{score} · {minute}", NotificationTone.Success),
            _ => (ar ? $"الماتش خلص: {score}" : $"Full time: {score}",
                ar ? "شوف إحصائيات الماتش." : "See the match stats.", NotificationTone.Info),
        };
        return new NotificationMessage(NotificationCategory.Matches, title, body, Link(a.FixtureId, a.KickoffAt), tone);
    }
}

/// <summary>Sends match alerts to the followers of both clubs (active people only), each in their own language.</summary>
public sealed class MatchAlertSender(AppDbContext db, INotifier notifier)
{
    public async Task SendAsync(IReadOnlyList<MatchAlert> alerts, CancellationToken ct)
    {
        if (alerts.Count == 0)
        {
            return;
        }
        var clubIds = alerts.SelectMany(a => new[] { a.Home.Id, a.Away.Id }).Distinct().ToList();
        var followers = await db.Set<FavoriteClub>().AsNoTracking()
            .Where(f => clubIds.Contains(f.ClubId) && f.User.Status == UserStatus.Active)
            .Select(f => new { f.ClubId, f.UserId, f.User.Language })
            .ToListAsync(ct);
        var items = new List<(Guid UserId, NotificationMessage Message)>();
        foreach (var a in alerts)
        {
            var people = followers.Where(f => f.ClubId == a.Home.Id || f.ClubId == a.Away.Id).DistinctBy(f => f.UserId);
            items.AddRange(people.Select(p => (p.UserId, MatchAlerts.Message(a, p.Language == "en" ? "en" : "ar"))));
        }
        if (items.Count > 0)
        {
            await notifier.NotifyManyAsync(items, ct);
        }
    }

    /// <summary>Line-ups are told once per match: true when its alert was already written.</summary>
    public Task<bool> LineupsToldAsync(long fixtureId, DateTime kickoffUtc, CancellationToken ct)
    {
        var link = MatchAlerts.Link(fixtureId, kickoffUtc);
        return db.Set<Notification>().AnyAsync(n => n.Category == NotificationCategory.Matches && n.Link == link
            && (n.Title.StartsWith("Line-ups are out") || n.Title.StartsWith("التشكيلة نزلت")), ct);
    }

    /// <summary>Whether anyone follows either club (no feed calls for line-ups nobody wants).</summary>
    public Task<bool> FollowedAsync(long homeClubId, long awayClubId, CancellationToken ct) =>
        db.Set<FavoriteClub>().AnyAsync(f => f.ClubId == homeClubId || f.ClubId == awayClubId, ct);
}
