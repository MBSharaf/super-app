using System.Net;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>The competitions on the matches page: the admin adds one from TheSportsDB, renames, hides and reorders it.</summary>
[Trait("Category", "Database")]
public sealed class CompetitionsAdminTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private static object Saudi(string color = "#006C35") =>
        new { key = "saudi", nameEn = "Saudi Pro League", nameAr = "دوري روشن السعودي", shortName = "Saudi", region = "National teams", color, theSportsDbId = 4668 };

    [Fact]
    public async Task An_admin_adds_a_competition_hides_it_and_customers_cannot()
    {
        var ceo = await api.CeoAsync();
        var list = (await ceo.GetAsync("/api/v1/admin/football/competitions")).Body.Data();
        list.GetArrayLength().ShouldBe(13);
        list.EnumerateArray().Single(c => c.GetProperty("key").GetString() == "epl").GetProperty("fantasyEnabled").GetBoolean().ShouldBeTrue();
        list.EnumerateArray().Single(c => c.GetProperty("key").GetString() == "egy").GetProperty("fantasyEnabled").GetBoolean().ShouldBeFalse();

        (await ceo.PostAsync("/api/v1/admin/football/competitions", Saudi("green"))).Status.ShouldBe(HttpStatusCode.BadRequest);
        var added = await ceo.PostAsync("/api/v1/admin/football/competitions", Saudi());
        added.Status.ShouldBe(HttpStatusCode.Created, added.Body.ToString());
        (added.Body.Data().GetProperty("provider").GetString(), added.Body.Data().GetProperty("fantasyEnabled").GetBoolean(), added.Body.Data().GetProperty("sortOrder").GetInt32())
            .ShouldBe(("thesportsdb", false, 14));
        (await ceo.PostAsync("/api/v1/admin/football/competitions", Saudi())).Body.Code().ShouldBe("competition_exists");
        (await (await api.ClientAsync()).GetAsync("/api/v1/matches/competitions")).Body.Data().EnumerateArray().ShouldContain(c => c.GetProperty("key").GetString() == "saudi");

        var hide = new { nameEn = "Saudi Pro League", nameAr = "دوري روشن", shortName = "KSA", region = "National teams", color = "#006C35", isActive = false, sortOrder = 3 };
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/football/competitions/saudi", hide)).Body.Data().GetProperty("isActive").GetBoolean().ShouldBeFalse();
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/football/competitions/nope", hide)).Status.ShouldBe(HttpStatusCode.NotFound);
        (await (await api.ClientAsync()).GetAsync("/api/v1/matches/competitions")).Body.Data().EnumerateArray().ShouldNotContain(c => c.GetProperty("key").GetString() == "saudi");

        var (customer, _) = await kit.PersonAsync(partner: false);
        (await customer.GetAsync("/api/v1/admin/football/competitions")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
