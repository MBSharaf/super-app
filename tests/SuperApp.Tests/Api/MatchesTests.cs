using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Tests.Api;

/// <summary>Real matches from the cache and Super App news with the admin's posts (API §6.2, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class MatchesTests(FantasyApiFactory api) : IClassFixture<FantasyApiFactory>
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
        var epl = (await client.GetAsync($"/api/v1/matches?date={cairo:yyyy-MM-dd}&competition=EPL")).Body.Data();
        epl.EnumerateArray().ShouldAllBe(m => m.GetProperty("competition").GetString() == "epl");
        var finished = day.EnumerateArray().First(m => m.GetProperty("status").GetString() == "Finished");
        var detail = (await client.GetAsync($"/api/v1/matches/{finished.GetProperty("id").GetInt64()}")).Body.Data();
        detail.GetProperty("players").GetArrayLength().ShouldBeGreaterThan(0);
        detail.GetProperty("match").GetProperty("home").GetProperty("shortName").GetString()!.Length.ShouldBe(3);
        (await client.GetAsync("/api/v1/matches/999999999")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/v1/matches?date=nope")).Status.ShouldBe(HttpStatusCode.BadRequest);
    }
}
