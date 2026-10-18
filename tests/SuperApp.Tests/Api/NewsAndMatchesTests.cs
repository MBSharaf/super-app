using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Tests.Api;

/// <summary>Real matches from the cache and Super App news with the admin's posts (API §6.2, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class NewsAndMatchesTests(FantasyApiFactory api) : IClassFixture<FantasyApiFactory>
{
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task Matches_of_a_day_and_a_match_with_its_players()
    {
        await api.EnsureSyncedAsync();
        var kickoff = await api.DbAsync(db => db.Set<Fixture>().Where(f => f.Status == Domain.Enums.FixtureStatus.Finished).OrderByDescending(f => f.KickoffAt).Select(f => f.KickoffAt).FirstAsync());
        var cairo = TimeZoneInfo.ConvertTimeFromUtc(kickoff, TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"));
        var client = await api.ClientAsync();
        var day = (await client.GetAsync($"/api/v1/matches?date={cairo:yyyy-MM-dd}")).Body.Data();
        day.GetArrayLength().ShouldBeGreaterThan(0);
        var egy = (await client.GetAsync($"/api/v1/matches?date={cairo:yyyy-MM-dd}&competition=EGY")).Body.Data();
        egy.EnumerateArray().ShouldAllBe(m => m.GetProperty("competition").GetString() == "egy");
        var finished = day.EnumerateArray().First(m => m.GetProperty("status").GetString() == "Finished");
        var detail = (await client.GetAsync($"/api/v1/matches/{finished.GetProperty("id").GetInt64()}")).Body.Data();
        detail.GetProperty("players").GetArrayLength().ShouldBeGreaterThan(0);
        detail.GetProperty("match").GetProperty("home").GetProperty("shortName").GetString()!.Length.ShouldBe(3);
        (await client.GetAsync("/api/v1/matches/999999999")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/v1/matches?date=nope")).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Admins_write_news_and_hidden_or_scheduled_posts_stay_out_of_the_public_list()
    {
        var ceo = await api.CeoAsync();
        var post = new { category = "league", title = "Cairo derby moved to Friday night", excerpt = "The league moved the derby by one day to give both clubs more rest.",
            body = "Full story here.", isBreaking = true, isHidden = false, publishedAt = (DateTime?)null };
        (await ceo.PostAsync("/api/v1/admin/news", post with { title = "Short" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var created = await ceo.PostAsync("/api/v1/admin/news", post);
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var id = created.Body.Data().GetProperty("id").GetInt64();
        (await ceo.PostAsync("/api/v1/admin/news", post with { title = "Hidden story for the team only", isHidden = true })).Status.ShouldBe(HttpStatusCode.Created);
        (await ceo.PostAsync("/api/v1/admin/news", post with { title = "Scheduled story for next week", publishedAt = (DateTime?)DateTime.UtcNow.AddDays(7) })).Status.ShouldBe(HttpStatusCode.Created);

        var guest = await api.ClientAsync();
        var list = (await guest.GetAsync("/api/v1/news?category=league")).Body;
        list.Data().GetArrayLength().ShouldBe(1);
        list.Data()[0].GetProperty("body").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Null);
        (await guest.GetAsync($"/api/v1/news/{id}")).Body.Data().GetProperty("body").GetString().ShouldBe("Full story here.");
        (await ceo.GetAsync("/api/v1/admin/news")).Body.Data().GetArrayLength().ShouldBe(3);

        var (customer, _) = await kit.PersonAsync(partner: false);
        (await customer.PostAsync("/api/v1/admin/news", post)).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await ceo.SendAsync(HttpMethod.Delete, $"/api/v1/admin/news/{id}")).Status.ShouldBe(HttpStatusCode.OK);
        (await guest.GetAsync($"/api/v1/news/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }
}
