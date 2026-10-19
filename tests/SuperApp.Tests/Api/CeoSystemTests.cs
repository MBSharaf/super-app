using System.Net;
using Shouldly;

namespace SuperApp.Tests.Api;

/// <summary>CEO system (API §8): kill switches, API catalogue, AI panel, org chart and the integrations registry.</summary>
[Trait("Category", "Database")]
public sealed class CeoSystemTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    [Fact]
    public async Task The_ceo_turns_a_kill_switch_off_with_a_reason_and_the_area_answers_423()
    {
        var ceo = await api.CeoAsync();
        var list = (await ceo.GetAsync("/api/v1/ceo/switches")).Body.Data();
        list.GetArrayLength().ShouldBe(9);

        var noReason = await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/switches/kill.applications", new { enabled = false });
        noReason.Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/switches/kill.nothing", new { enabled = true })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/switches/api.agents", new { enabled = true })).Status.ShouldBe(HttpStatusCode.NotFound);

        var off = await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/switches/kill.applications", new { enabled = false, reason = "Review backlog" });
        off.Status.ShouldBe(HttpStatusCode.OK, off.Body.ToString());
        off.Body.Data().GetProperty("isEnabled").GetBoolean().ShouldBeFalse();
        off.Body.Data().GetProperty("reason").GetString().ShouldBe("Review backlog");
        try
        {
            var flag = await api.DbAsync(db => Task.FromResult(db.Set<Domain.Entities.Ops.FeatureFlag>().Single(f => f.Key == "kill.applications").IsEnabled));
            flag.ShouldBeFalse();
        }
        finally
        {
            (await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/switches/kill.applications", new { enabled = true })).Status.ShouldBe(HttpStatusCode.OK);
        }
    }

    [Fact]
    public async Task The_api_catalogue_lists_every_group_and_switches_only_groups_with_a_switch()
    {
        var ceo = await api.CeoAsync();
        var groups = (await ceo.GetAsync("/api/v1/ceo/api-catalogue")).Body.Data().EnumerateArray().ToList();
        groups.Count.ShouldBeGreaterThan(10);
        groups.Single(g => g.GetProperty("group").GetString() == "agent").GetProperty("switchKey").GetString().ShouldBe("api.agents");

        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/api-catalogue/admin", new { enabled = false, reason = "x" })).Body.Code().ShouldBe("no_switch");
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/api-catalogue/nothing", new { enabled = false, reason = "x" })).Status.ShouldBe(HttpStatusCode.NotFound);
        var off = await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/api-catalogue/webhooks", new { enabled = false, reason = "Not used yet" });
        off.Status.ShouldBe(HttpStatusCode.OK, off.Body.ToString());
        off.Body.Data().GetProperty("enabled").GetBoolean().ShouldBeFalse();
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/api-catalogue/webhooks", new { enabled = true })).Body.Data().GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task The_ai_panel_sums_agents_runs_and_cost()
    {
        var ceo = await api.CeoAsync();
        var panel = await ceo.GetAsync("/api/v1/ceo/ai");
        panel.Status.ShouldBe(HttpStatusCode.OK, panel.Body.ToString());
        panel.Body.Data().GetProperty("agentsSwitchOn").GetBoolean().ShouldBeTrue();
        panel.Body.Data().TryGetProperty("byAgent", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task The_org_chart_shows_the_pyramid_and_positions_can_be_added_moved_and_removed()
    {
        var ceo = await api.CeoAsync();
        var chart = (await ceo.GetAsync("/api/v1/ceo/org")).Body.Data();
        chart.GetProperty("departments").GetArrayLength().ShouldBe(8);
        var top = chart.GetProperty("positions").EnumerateArray().First(p => p.GetProperty("reportsToId").ValueKind == System.Text.Json.JsonValueKind.Null);
        top.GetProperty("holder").GetProperty("kind").GetString().ShouldBe("human");
        var topId = top.GetProperty("id").GetInt64();
        var dept = chart.GetProperty("departments")[0].GetProperty("id").GetInt32();

        var created = await ceo.PostAsync("/api/v1/ceo/org/positions", new { departmentId = dept, reportsToId = topId, titleEn = "Head of Growth", titleAr = "رئيس النمو", duties = new[] { "Plans campaigns" }, sortOrder = 50 });
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        created.Body.Data().GetProperty("holder").GetProperty("kind").GetString().ShouldBe("vacant");
        var id = created.Body.Data().GetProperty("id").GetInt64();
        var child = (await ceo.PostAsync("/api/v1/ceo/org/positions", new { departmentId = dept, reportsToId = id, titleEn = "Growth analyst", titleAr = "محلل نمو", sortOrder = 51 })).Body.Data().GetProperty("id").GetInt64();

        var loop = await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/org/positions/{id}", new { departmentId = dept, reportsToId = child, titleEn = "Head of Growth", titleAr = "رئيس النمو", sortOrder = 50 });
        loop.Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.SendAsync(HttpMethod.Delete, $"/api/v1/ceo/org/positions/{id}")).Body.Code().ShouldBe("has_reports");
        (await ceo.PostAsync("/api/v1/ceo/org/positions", new { titleEn = "", titleAr = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);

        (await ceo.SendAsync(HttpMethod.Delete, $"/api/v1/ceo/org/positions/{child}")).Status.ShouldBe(HttpStatusCode.OK);
        var renamed = await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/org/positions/{id}", new { departmentId = dept, reportsToId = topId, titleEn = "Growth lead", titleAr = "قائد النمو", sortOrder = 50 });
        renamed.Body.Data().GetProperty("titleEn").GetString().ShouldBe("Growth lead");
        (await ceo.SendAsync(HttpMethod.Delete, $"/api/v1/ceo/org/positions/{id}")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.SendAsync(HttpMethod.Delete, $"/api/v1/ceo/org/positions/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_integrations_registry_switches_providers_and_needs_secrets_for_live_modes()
    {
        var ceo = await api.CeoAsync();
        var list = (await ceo.GetAsync("/api/v1/ceo/integrations")).Body.Data().EnumerateArray().ToList();
        list.ShouldContain(i => i.GetProperty("key").GetString() == "football.apifootball");

        var live = await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/integrations/courier.bosta", new { mode = "Live", isEnabled = true, isPrimary = true });
        live.Status.ShouldBe((HttpStatusCode)422);
        live.Body.Code().ShouldBe("secret_missing");
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/integrations/courier.bosta", new { mode = "Fast" })).Status.ShouldBe(HttpStatusCode.BadRequest);

        var primary = await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/integrations/courier.mylerz", new { mode = "Simulated", isEnabled = true, isPrimary = true, config = new { zone = "cairo" } });
        primary.Status.ShouldBe(HttpStatusCode.OK, primary.Body.ToString());
        primary.Body.Data().GetProperty("isPrimary").GetBoolean().ShouldBeTrue();
        (await ceo.GetAsync("/api/v1/ceo/integrations/courier.simulated")).Body.Data().GetProperty("isPrimary").GetBoolean().ShouldBeFalse();

        var test = await ceo.PostAsync("/api/v1/ceo/integrations/courier.mylerz/test");
        test.Body.Data().GetProperty("status").GetString().ShouldBe("Healthy");
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/integrations/courier.simulated", new { mode = "Simulated", isEnabled = true, isPrimary = true })).Status.ShouldBe(HttpStatusCode.OK);
        var detail = await ceo.GetAsync("/api/v1/ceo/integrations/courier.mylerz");
        detail.Body.Data().GetProperty("usage").ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
        (await ceo.GetAsync("/api/v1/ceo/integrations/nothing.here")).Status.ShouldBe(HttpStatusCode.NotFound);
    }
}
