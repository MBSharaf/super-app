using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Football;

namespace SuperApp.Tests.Api;

/// <summary>Favourite clubs (up to three), their match alerts, news stories with their alerts, the bell and the phone switches.</summary>
[Trait("Category", "Database")]
public sealed class FavoritesNewsTests(FantasyApiFactory api) : IClassFixture<FantasyApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private async Task<List<long>> ClubIdsAsync()
    {
        await api.EnsureSyncedAsync();
        var comps = (await (await api.ClientAsync()).GetAsync("/api/v1/matches/clubs")).Body.Data();
        return comps.EnumerateArray().SelectMany(c => c.GetProperty("clubs").EnumerateArray()).Select(c => c.GetProperty("id").GetInt64()).Distinct().ToList();
    }

    [Fact]
    public async Task Up_to_three_clubs_in_order_and_only_from_the_list()
    {
        var clubs = await ClubIdsAsync();
        clubs.Count.ShouldBeGreaterThan(4);
        var (me, _) = await kit.PersonAsync(partner: false);
        (await me.GetAsync("/api/v1/me/favorite-clubs")).Body.Data().GetArrayLength().ShouldBe(0);

        var saved = await me.SendAsync(HttpMethod.Put, "/api/v1/me/favorite-clubs", new { clubIds = new[] { clubs[2], clubs[0], clubs[1] } });
        saved.Status.ShouldBe(HttpStatusCode.OK, saved.Body.ToString());
        saved.Body.Data().EnumerateArray().Select(c => c.GetProperty("id").GetInt64()).ShouldBe([clubs[2], clubs[0], clubs[1]]);

        (await me.SendAsync(HttpMethod.Put, "/api/v1/me/favorite-clubs", new { clubIds = clubs.Take(4) })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await me.SendAsync(HttpMethod.Put, "/api/v1/me/favorite-clubs", new { clubIds = new[] { 987654321L } })).Status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await me.SendAsync(HttpMethod.Put, "/api/v1/me/favorite-clubs", new { clubIds = new[] { clubs[0] } })).Body.Data().GetArrayLength().ShouldBe(1);
        (await (await api.ClientAsync()).SendAsync(HttpMethod.Put, "/api/v1/me/favorite-clubs", new { clubIds = new[] { clubs[0] } })).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Followers_get_match_alerts_in_their_language_and_line_ups_once()
    {
        var clubs = await ClubIdsAsync();
        var (fan, fanId) = await kit.PersonAsync(partner: false);
        var (_, otherId) = await kit.PersonAsync(partner: false);
        await fan.SendAsync(HttpMethod.Put, "/api/v1/me/favorite-clubs", new { clubIds = new[] { clubs[0] } });
        var fixture = await api.DbAsync(db => db.Set<Fixture>().Include(f => f.HomeClub).Include(f => f.AwayClub)
            .FirstAsync(f => f.HomeClubId == clubs[0] || f.AwayClubId == clubs[0]));

        using (var scope = api.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<MatchAlertSender>();
            var home = new AlertClub(fixture.HomeClubId, fixture.HomeClub.NameEn, "الفريق");
            var away = new AlertClub(fixture.AwayClubId, fixture.AwayClub.NameEn, "");
            (await sender.LineupsToldAsync(fixture.Id, fixture.KickoffAt, default)).ShouldBeFalse();
            await sender.SendAsync([new MatchAlert(fixture.Id, fixture.KickoffAt, home, away, MatchMoment.Lineups, null, null, null),
                new MatchAlert(fixture.Id, fixture.KickoffAt, home, away, MatchMoment.Goal, 1, 0, 23, HomeScored: true)], default);
            (await sender.LineupsToldAsync(fixture.Id, fixture.KickoffAt, default)).ShouldBeTrue();
        }

        var bell = (await fan.GetAsync("/api/v1/me/notifications")).Body.Data();
        bell.GetProperty("unread").GetInt32().ShouldBe(2);
        var goal = bell.GetProperty("items").EnumerateArray().First();
        goal.GetProperty("category").GetString().ShouldBe("matches");
        goal.GetProperty("title").GetString().ShouldBe("⚽ جووول الفريق!");   // Arabic by default, with the club's Arabic name
        goal.GetProperty("link").GetString()!.ShouldContain($"open=m-{fixture.Id}");
        (await api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.UserId == otherId && n.Category == NotificationCategory.Matches))).ShouldBeFalse();

        (await fan.PostAsync("/api/v1/me/notifications/read", new { ids = new[] { goal.GetProperty("id").GetInt64() } })).Status.ShouldBe(HttpStatusCode.OK);
        (await fan.GetAsync("/api/v1/me/notifications")).Body.Data().GetProperty("unread").GetInt32().ShouldBe(1);
        await fan.PostAsync("/api/v1/me/notifications/read", new { });
        (await fan.GetAsync("/api/v1/me/notifications")).Body.Data().GetProperty("unread").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task News_reaches_the_clubs_followers_or_everyone_when_important_and_hidden_stories_go()
    {
        var clubs = await ClubIdsAsync();
        var ceo = await api.CeoAsync();
        var (fan, fanId) = await kit.PersonAsync(partner: false);
        var (stranger, strangerId) = await kit.PersonAsync(partner: false);
        await fan.SendAsync(HttpMethod.Put, "/api/v1/me/favorite-clubs", new { clubIds = new[] { clubs[1] } });

        var club = await ceo.PostAsync("/api/v1/admin/news", new { title = "Coach leaves after the derby", body = "The club confirmed it tonight.", language = "en",
            sourceName = "Club site", sourceUrl = "https://example.com/story", clubId = clubs[1], important = false });
        club.Status.ShouldBe(HttpStatusCode.Created, club.Body.ToString());
        var id = club.Body.Data().GetProperty("item").GetProperty("id").GetInt64();
        (await ceo.PostAsync("/api/v1/admin/news", new { title = "x", body = "y", language = "en", sourceUrl = "http://plain.example" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await fan.PostAsync("/api/v1/admin/news", new { title = "x", body = "y", language = "en" })).Status.ShouldBe(HttpStatusCode.Forbidden);

        Task<int> NewsFor(Guid user) => api.DbAsync(db => db.Set<Notification>().CountAsync(n => n.UserId == user && n.Category == NotificationCategory.News));
        (await NewsFor(fanId)).ShouldBe(1);
        (await NewsFor(strangerId)).ShouldBe(0);
        await ceo.PostAsync("/api/v1/admin/news", new { title = "League suspended this week", body = "Official statement.", language = "en", important = true });
        (await NewsFor(strangerId)).ShouldBe(1);

        var visitor = await api.ClientAsync();
        (await visitor.GetAsync($"/api/v1/news/{id}")).Body.Data().GetProperty("body").GetString().ShouldBe("The club confirmed it tonight.");
        (await ceo.SendAsync(HttpMethod.Delete, $"/api/v1/admin/news/{id}")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.GetAsync($"/api/v1/admin/news/{id}")).Body.Data().GetProperty("published").GetBoolean().ShouldBeFalse();
        (await stranger.GetAsync($"/api/v1/news?club={clubs[1]}&page=2")).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Phone_switches_per_kind()
    {
        var (me, _) = await kit.PersonAsync(partner: false);
        var all = (await me.GetAsync("/api/v1/me/notification-settings")).Body.Data();
        all.EnumerateArray().ShouldAllBe(s => s.GetProperty("push").GetBoolean());
        var saved = await me.SendAsync(HttpMethod.Put, "/api/v1/me/notification-settings", new { settings = new[] { new { category = "matches", push = false } } });
        saved.Status.ShouldBe(HttpStatusCode.OK, saved.Body.ToString());
        saved.Body.Data().EnumerateArray().Single(s => s.GetProperty("category").GetString() == "matches").GetProperty("push").GetBoolean().ShouldBeFalse();
        (await me.SendAsync(HttpMethod.Put, "/api/v1/me/notification-settings", new { settings = new[] { new { category = "system", push = false } } })).Status.ShouldBe(HttpStatusCode.BadRequest);
    }
}
