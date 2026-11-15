using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>AI agents and their API keys (API §8 "AI agents", §9.1 agent API): CEO controls, X-Api-Key sign-in, limits, audit.</summary>
[Trait("Category", "Database")]
public sealed class AgentKeysTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private static object NewAgent(string name, params string[] permissions) => new
    {
        name, titleEn = "Support agent", titleAr = "وكيل الدعم", description = "Answers tickets", model = "claude-sonnet-5-5",
        platform = "N8n", moneyLimit = 0, monthlyBudget = 50, permissions, positionId = (long?)null,
    };

    /// <summary>Creates an agent, gives it a key and resumes it; returns its id and the key.</summary>
    private static async Task<(long Id, string Key)> ActiveAgentAsync(ApiClient ceo, int? rate = null, params string[] permissions)
    {
        var name = "Agent " + Guid.NewGuid().ToString("N")[..8];
        var created = await ceo.PostAsync("/api/v1/ceo/agents", NewAgent(name, permissions.Length == 0 ? ["support.read"] : permissions));
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var id = created.Body.Data().GetProperty("agent").GetProperty("id").GetInt64();
        var key = await ceo.PostAsync($"/api/v1/ceo/agents/{id}/keys", new { name = "n8n", rateLimitPerMinute = rate });
        key.Status.ShouldBe(HttpStatusCode.Created, key.Body.ToString());
        (await ceo.PostAsync($"/api/v1/ceo/agents/{id}/resume")).Status.ShouldBe(HttpStatusCode.OK);
        return (id, key.Body.Data().GetProperty("key").GetString()!);
    }

    private ApiClient AgentClient(string key)
    {
        var http = api.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        http.DefaultRequestHeaders.Add("X-Api-Key", key);
        return new ApiClient(http);
    }

    [Fact]
    public async Task The_ceo_creates_an_agent_that_starts_paused_and_the_key_is_shown_once()
    {
        var ceo = await api.CeoAsync();
        var created = await ceo.PostAsync("/api/v1/ceo/agents", NewAgent("Helper " + Guid.NewGuid().ToString("N")[..6], "support.read", "support.reply"));
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var agent = created.Body.Data().GetProperty("agent");
        agent.GetProperty("status").GetString().ShouldBe("Paused");
        agent.GetProperty("permissions").GetArrayLength().ShouldBe(2);
        var id = agent.GetProperty("id").GetInt64();

        var key = await ceo.PostAsync($"/api/v1/ceo/agents/{id}/keys", new { name = "n8n production" });
        key.Status.ShouldBe(HttpStatusCode.Created);
        var secret = key.Body.Data().GetProperty("key").GetString()!;
        secret.ShouldStartWith("sa_");
        key.Body.Data().GetProperty("rateLimitPerMinute").GetInt32().ShouldBe(600);

        var detail = (await ceo.GetAsync($"/api/v1/ceo/agents/{id}")).Body.Data();
        detail.GetProperty("keys")[0].GetProperty("prefix").GetString().ShouldBe(secret[..8]);
        detail.GetProperty("keys")[0].TryGetProperty("key", out _).ShouldBeFalse();
        var stored = await api.DbAsync(db => db.Set<Domain.Entities.Ai.ApiKey>().SingleAsync(k => k.AgentId == id));
        stored.KeyHash.Length.ShouldBe(32);

        var paused = await AgentClient(secret).GetAsync("/api/v1/agent/me");
        paused.Status.ShouldBe(HttpStatusCode.Locked);
        paused.Body.Code().ShouldBe("agent_paused");
    }

    [Fact]
    public async Task An_active_key_calls_the_agent_api_and_admin_endpoints_it_holds_the_permission_for()
    {
        var ceo = await api.CeoAsync();
        var (id, key) = await ActiveAgentAsync(ceo);
        var agent = AgentClient(key);

        var me = await agent.GetAsync("/api/v1/agent/me");
        me.Status.ShouldBe(HttpStatusCode.OK, me.Body.ToString());
        me.Body.Data().GetProperty("id").GetInt64().ShouldBe(id);
        me.Body.Data().GetProperty("keyPrefix").GetString().ShouldBe(key[..8]);

        (await agent.GetAsync("/api/v1/admin/support/tickets")).Status.ShouldBe(HttpStatusCode.OK);
        var notAllowed = await agent.GetAsync("/api/v1/admin/community/reports");
        notAllowed.Status.ShouldBe(HttpStatusCode.Forbidden);
        notAllowed.Body.Code().ShouldBe("forbidden");
        (await agent.GetAsync("/api/v1/ceo/agents")).Status.ShouldBe(HttpStatusCode.Forbidden);

        var audit = await api.DbAsync(db => db.Set<AuditLog>().Where(a => a.ActorAgentId == id).ToListAsync());
        audit.ShouldContain(a => a.Action == "agent.call" && a.System == AuditSystem.ApiOrAgent && a.EntityId == "admin");
    }

    [Fact]
    public async Task Agents_report_runs_without_csrf_and_the_budget_pauses_them()
    {
        var ceo = await api.CeoAsync();
        var (id, key) = await ActiveAgentAsync(ceo);
        var agent = AgentClient(key);

        var run = await agent.PostAsync("/api/v1/agent/runs", new { summary = "Answering tickets" });
        run.Status.ShouldBe(HttpStatusCode.Created, run.Body.ToString());
        var runId = run.Body.Data().GetProperty("id").GetInt64();
        var bad = await agent.SendAsync(HttpMethod.Patch, $"/api/v1/agent/runs/{runId}", new { result = "Exploded", inputTokens = 1, outputTokens = 1, costUsd = 1 });
        bad.Status.ShouldBe(HttpStatusCode.BadRequest);
        var done = await agent.SendAsync(HttpMethod.Patch, $"/api/v1/agent/runs/{runId}", new { result = "Done", summary = "3 answered", inputTokens = 900, outputTokens = 300, costUsd = 10 });
        done.Status.ShouldBe(HttpStatusCode.OK, done.Body.ToString());
        (await agent.SendAsync(HttpMethod.Patch, $"/api/v1/agent/runs/{runId}", new { result = "Done", inputTokens = 1, outputTokens = 1, costUsd = 1 })).Status.ShouldBe(HttpStatusCode.Conflict);
        var second = (await agent.PostAsync("/api/v1/agent/runs", new { summary = "Big batch" })).Body.Data().GetProperty("id").GetInt64();
        (await agent.SendAsync(HttpMethod.Patch, $"/api/v1/agent/runs/{second}", new { result = "Done", inputTokens = 9000, outputTokens = 3000, costUsd = 50 })).Status.ShouldBe(HttpStatusCode.OK);

        var card = (await ceo.GetAsync($"/api/v1/ceo/agents/{id}")).Body.Data().GetProperty("agent");
        card.GetProperty("status").GetString().ShouldBe("Paused");
        card.GetProperty("costThisMonthUsd").GetDecimal().ShouldBe(60);
        (await agent.GetAsync("/api/v1/agent/tasks")).Status.ShouldBe(HttpStatusCode.Locked);
    }

    [Fact]
    public async Task Revoked_and_wrong_keys_get_401_and_the_cookie_still_works_without_a_key()
    {
        var ceo = await api.CeoAsync();
        var (id, key) = await ActiveAgentAsync(ceo);
        (await AgentClient("sa_" + new string('x', 40)).GetAsync("/api/v1/agent/me")).Body.Code().ShouldBe("invalid_api_key");
        (await AgentClient("nonsense").GetAsync("/api/v1/agent/me")).Status.ShouldBe(HttpStatusCode.Unauthorized);

        var keyId = (await ceo.GetAsync($"/api/v1/ceo/agents/{id}")).Body.Data().GetProperty("keys")[0].GetProperty("id").GetInt64();
        (await ceo.PostAsync($"/api/v1/ceo/agents/{id}/keys/{keyId}/revoke")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/ceo/agents/{id}/keys/{keyId}/revoke")).Status.ShouldBe(HttpStatusCode.Conflict);
        var revoked = await AgentClient(key).GetAsync("/api/v1/agent/me");
        revoked.Status.ShouldBe(HttpStatusCode.Unauthorized);
        revoked.Body.Code().ShouldBe("invalid_api_key");

        (await ceo.GetAsync("/api/v1/ceo/agents")).Status.ShouldBe(HttpStatusCode.OK);
        (await (await api.ClientAsync()).GetAsync("/api/v1/agent/me")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Each_key_has_its_own_rate_limit()
    {
        var ceo = await api.CeoAsync();
        var (_, key) = await ActiveAgentAsync(ceo, rate: 2);
        var agent = AgentClient(key);
        (await agent.GetAsync("/api/v1/agent/me")).Status.ShouldBe(HttpStatusCode.OK);
        (await agent.GetAsync("/api/v1/agent/me")).Status.ShouldBe(HttpStatusCode.OK);
        var third = await agent.GetAsync("/api/v1/agent/me");
        third.Status.ShouldBe(HttpStatusCode.TooManyRequests);
        third.Body.Code().ShouldBe("rate_limited");
    }

    [Fact]
    public async Task Emergency_stop_locks_every_key_until_released()
    {
        var ceo = await api.CeoAsync();
        var (id, key) = await ActiveAgentAsync(ceo);
        (await ceo.PostAsync("/api/v1/ceo/agents/emergency-stop", new { reason = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var stop = await ceo.PostAsync("/api/v1/ceo/agents/emergency-stop", new { reason = "Test drill" });
        stop.Status.ShouldBe(HttpStatusCode.OK, stop.Body.ToString());
        try
        {
            var locked = await AgentClient(key).GetAsync("/api/v1/agent/me");
            locked.Status.ShouldBe(HttpStatusCode.Locked);
            locked.Body.Code().ShouldBe("agents_stopped");
            (await ceo.PostAsync($"/api/v1/ceo/agents/{id}/resume")).Body.Code().ShouldBe("agents_stopped");
            var switches = (await ceo.GetAsync("/api/v1/ceo/switches")).Body.Data().EnumerateArray();
            switches.Single(s => s.GetProperty("key").GetString() == "kill.agents").GetProperty("isEnabled").GetBoolean().ShouldBeFalse();
        }
        finally
        {
            (await ceo.PostAsync("/api/v1/ceo/agents/emergency-release")).Status.ShouldBe(HttpStatusCode.OK);
        }
        (await AgentClient(key).GetAsync("/api/v1/agent/me")).Status.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task The_ceo_edits_permissions_tasks_and_deletes_an_agent()
    {
        var ceo = await api.CeoAsync();
        var (id, key) = await ActiveAgentAsync(ceo);
        (await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/agents/{id}/permissions", new { permissions = new[] { "payouts.send" } })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/agents/{id}/permissions", new { permissions = new[] { "nope.code" } })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/agents/{id}/permissions", new { permissions = new[] { "community.moderate" } })).Status.ShouldBe(HttpStatusCode.OK);
        (await AgentClient(key).GetAsync("/api/v1/admin/community/reports")).Status.ShouldBe(HttpStatusCode.OK);

        var tasks = await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/agents/{id}/tasks", new
        {
            tasks = new object[]
            {
                new { title = "Morning sweep", description = "Check reports", trigger = "Schedule", cron = "0 7 * * *" },
                new { title = "New report", description = "Look at it", trigger = "Event", eventType = "community.reported" },
            },
        });
        tasks.Status.ShouldBe(HttpStatusCode.OK, tasks.Body.ToString());
        (await AgentClient(key).GetAsync("/api/v1/agent/tasks")).Body.Data().GetArrayLength().ShouldBe(2);
        (await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/agents/{id}/tasks", new { tasks = new[] { new { title = "X", description = "", trigger = "Schedule" } } }))
            .Status.ShouldBe(HttpStatusCode.BadRequest);

        var edit = await ceo.SendAsync(HttpMethod.Put, $"/api/v1/ceo/agents/{id}", new
        {
            name = "Renamed " + id, titleEn = "Moderator", titleAr = "مشرف", description = "", model = "claude-opus-5-5", platform = "DirectApi", moneyLimit = 0, monthlyBudget = 10,
        });
        edit.Status.ShouldBe(HttpStatusCode.OK, edit.Body.ToString());
        (await ceo.PostAsync($"/api/v1/ceo/agents/{id}/pause", new { reason = "Night" })).Body.Data().GetProperty("agent").GetProperty("status").GetString().ShouldBe("Paused");

        (await ceo.SendAsync(HttpMethod.Delete, $"/api/v1/ceo/agents/{id}")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.GetAsync($"/api/v1/ceo/agents/{id}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await AgentClient(key).GetAsync("/api/v1/agent/me")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Agent_names_are_unique_and_guests_cannot_open_the_ai_panel()
    {
        var ceo = await api.CeoAsync();
        var name = "Twin " + Guid.NewGuid().ToString("N")[..6];
        (await ceo.PostAsync("/api/v1/ceo/agents", NewAgent(name, "support.read"))).Status.ShouldBe(HttpStatusCode.Created);
        var twin = await ceo.PostAsync("/api/v1/ceo/agents", NewAgent(name, "support.read"));
        twin.Status.ShouldBe(HttpStatusCode.Conflict);
        twin.Body.Code().ShouldBe("name_taken");
        (await ceo.PostAsync("/api/v1/ceo/agents", new { name = "", platform = "Robot" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await (await api.ClientAsync()).GetAsync("/api/v1/ceo/agents")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
