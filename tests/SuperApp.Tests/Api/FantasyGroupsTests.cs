using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>Private groups, official sponsor leagues with the Fantasy Pass, paid groups, sponsors and winners (API §6.1, §7.2).</summary>
[Trait("Category", "Database")]
public sealed class FantasyGroupsTests(FantasyApiFactory api) : IClassFixture<FantasyApiFactory>
{
    private readonly PartnerKit kit = new(api);

    /// <summary>A signed-in person with a team in the Egyptian league (or without one).</summary>
    private async Task<(ApiClient Client, Guid UserId)> ManagerAsync(bool withTeam = true)
    {
        await api.EnsureSyncedAsync();
        var (client, userId) = await kit.PersonAsync(partner: false);
        if (withTeam)
        {
            var boot = (await client.GetAsync("/api/v1/fantasy/EGY/bootstrap")).Body.Data();
            var created = await client.PostAsync("/api/v1/fantasy/EGY/team", new { name = "Group Test XI", picks = FantasyKit.CheapSquad(boot).Select(p => p.Id()).ToArray() });
            created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        }
        return (client, userId);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> BuyPassAsync(ApiClient client, string plan, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/fantasy/pass") { Content = JsonContent.Create(new { plan }) };
        if (key is not null)
        {
            request.Headers.Add("Idempotency-Key", key);
        }
        var response = await client.Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, JsonDocument.Parse(text).RootElement.Clone());
    }

    [Fact]
    public async Task A_private_group_is_joined_by_its_code_and_only_members_see_the_table()
    {
        var (lonely, _) = await ManagerAsync(withTeam: false);
        (await lonely.PostAsync("/api/v1/fantasy/EGY/groups", new { name = "No Team League" })).Body.Code().ShouldBe("no_team");

        var (owner, _) = await ManagerAsync();
        (await owner.PostAsync("/api/v1/fantasy/EGY/groups", new { name = "ab" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var created = await owner.PostAsync("/api/v1/fantasy/EGY/groups", new { name = "Smart Village Office" });
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var group = created.Body.Data();
        var code = group.GetProperty("inviteCode").GetString()!;
        code.ShouldStartWith("SA-");
        (group.GetProperty("isOwner").GetBoolean(), group.GetProperty("members").GetInt32()).ShouldBe((true, 1));
        var id = group.GetProperty("id").GetInt64();

        (await lonely.PostAsync("/api/v1/fantasy/groups/join", new { inviteCode = code })).Body.Code().ShouldBe("no_team");
        (await lonely.GetAsync($"/api/v1/fantasy/groups/{id}/table")).Body.Code().ShouldBe("not_member");
        var (friend, _) = await ManagerAsync();
        (await friend.PostAsync("/api/v1/fantasy/groups/join", new { inviteCode = "SA-NOPE00" })).Status.ShouldBe(HttpStatusCode.NotFound);
        (await friend.PostAsync("/api/v1/fantasy/groups/join", new { inviteCode = code.ToLowerInvariant() })).Status.ShouldBe(HttpStatusCode.OK);
        (await friend.PostAsync("/api/v1/fantasy/groups/join", new { inviteCode = code })).Body.Code().ShouldBe("already_member");
        var table = (await friend.GetAsync($"/api/v1/fantasy/groups/{id}/table")).Body.Data();
        table.GetProperty("rows").GetArrayLength().ShouldBe(2);
        table.GetProperty("rows").EnumerateArray().Count(r => r.GetProperty("isMe").GetBoolean()).ShouldBe(1);
        table.GetProperty("rows")[0].GetProperty("manager").GetString().ShouldBe("Test P.");

        var overview = (await friend.GetAsync("/api/v1/fantasy/EGY/groups")).Body.Data();
        overview.GetProperty("mine").EnumerateArray().ShouldContain(g => g.GetProperty("id").GetInt64() == id);
        overview.GetProperty("overall").GetProperty("hasTeam").GetBoolean().ShouldBeTrue();
        overview.GetProperty("paid").GetArrayLength().ShouldBe(0);

        (await friend.PostAsync($"/api/v1/fantasy/groups/{id}/leave")).Status.ShouldBe(HttpStatusCode.OK);
        (await friend.GetAsync($"/api/v1/fantasy/groups/{id}/table")).Body.Code().ShouldBe("not_member");
        (await friend.PostAsync($"/api/v1/fantasy/groups/{id}/leave")).Body.Code().ShouldBe("not_member");
    }

    [Fact]
    public async Task Official_prize_leagues_need_the_fantasy_pass_and_paid_groups_wait_for_payments()
    {
        var ceo = await api.CeoAsync();
        var sponsorBody = new { name = "Nile Cola", tier = "Gold", contactEmail = "deals@nilecola.example", contractValue = 60000m, startsOn = "2026-08-01", endsOn = "2027-05-31", color = "#C8102E" };
        (await ceo.PostAsync("/api/v1/admin/fantasy/sponsors", sponsorBody with { })).Status.ShouldBe(HttpStatusCode.Created);
        (await ceo.PostAsync("/api/v1/admin/fantasy/sponsors", sponsorBody)).Body.Code().ShouldBe("sponsor_exists");
        (await ceo.PostAsync("/api/v1/admin/fantasy/sponsors", new { name = "Bad Colour", tier = "Gold", contactEmail = (string?)null, contractValue = 0m, startsOn = "2026-08-01", endsOn = "2026-07-01", color = "red" }))
            .Status.ShouldBe(HttpStatusCode.BadRequest);
        var sponsorId = (await ceo.GetAsync("/api/v1/admin/fantasy/sponsors")).Body.Data().EnumerateArray().Single(s => s.GetProperty("name").GetString() == "Nile Cola").GetProperty("id").GetInt64();

        var league = new { competition = "egy", name = "Cairo Derby Cup", type = "Official", sponsorId = (long?)sponsorId, prizeText = "Two derby tickets + signed kit", prizeValue = 60000m, monthlyPrice = (decimal?)null };
        (await ceo.PostAsync("/api/v1/admin/fantasy/groups", league with { prizeText = "5,000 EGP cash" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var created = await ceo.PostAsync("/api/v1/admin/fantasy/groups", league);
        created.Status.ShouldBe(HttpStatusCode.Created, created.Body.ToString());
        var code = created.Body.Data().GetProperty("inviteCode").GetString();
        var id = created.Body.Data().GetProperty("id").GetInt64();
        (await ceo.PostAsync("/api/v1/admin/fantasy/groups", league with { type = "Paid", monthlyPrice = (decimal?)30m })).Body.Code().ShouldBe("payments_not_live");

        var (player, userId) = await ManagerAsync();
        (await player.PostAsync("/api/v1/admin/fantasy/groups", league)).Status.ShouldBe(HttpStatusCode.Forbidden);
        var official = (await player.GetAsync("/api/v1/fantasy/EGY/groups")).Body.Data().GetProperty("official").EnumerateArray().Single(g => g.GetProperty("id").GetInt64() == id);
        (official.GetProperty("needsPass").GetBoolean(), official.GetProperty("sponsor").GetProperty("name").GetString()).ShouldBe((true, "Nile Cola"));
        (await player.PostAsync("/api/v1/fantasy/groups/join", new { inviteCode = code })).Body.Code().ShouldBe("pass_required");

        var status = (await player.GetAsync("/api/v1/fantasy/pass")).Body.Data();
        status.GetProperty("plans").EnumerateArray().Select(p => (p.GetProperty("plan").GetString(), p.GetProperty("price").GetDecimal())).ShouldBe([("egy", 50m), ("epl", 50m), ("both", 80m)]);
        (await BuyPassAsync(player, "egy", null)).Body.Code().ShouldBe("idempotency_key");
        (await BuyPassAsync(player, "gold", "k-1")).Body.Code().ShouldBe("unknown_plan");
        var bought = await BuyPassAsync(player, "egy", "k-2");
        bought.Status.ShouldBe(HttpStatusCode.OK, bought.Body.ToString());
        bought.Body.Data().GetProperty("coversUntil").GetProperty("egy").ValueKind.ShouldBe(JsonValueKind.String);
        bought.Body.Data().GetProperty("coversUntil").GetProperty("epl").ValueKind.ShouldBe(JsonValueKind.Null);
        (await BuyPassAsync(player, "egy", "k-2")).Status.ShouldBe(HttpStatusCode.OK);
        (await api.DbAsync(db => db.Set<FantasyPass>().CountAsync(p => p.UserId == userId))).ShouldBe(1);

        (await player.PostAsync("/api/v1/fantasy/groups/join", new { inviteCode = code })).Status.ShouldBe(HttpStatusCode.OK);
        var table = await (await api.ClientAsync()).GetAsync($"/api/v1/fantasy/groups/{id}/table");
        table.Status.ShouldBe(HttpStatusCode.OK);
        table.Body.Data().GetProperty("rows").GetArrayLength().ShouldBe(1);
    }

    [Fact]
    public async Task Winners_get_shop_vouchers_once()
    {
        var ceo = await api.CeoAsync();
        var created = await ceo.PostAsync("/api/v1/admin/fantasy/groups", new { competition = "EGY", name = "Gameweek Sprint", type = "Official", sponsorId = (long?)null,
            prizeText = "1,000 EGP shop voucher", prizeValue = 1000m, monthlyPrice = (decimal?)null });
        var id = created.Body.Data().GetProperty("id").GetInt64();
        var (player, userId) = await ManagerAsync();
        var team = await api.DbAsync(db => db.Set<FantasyTeam>().SingleAsync(t => t.UserId == userId));
        await api.DbAsync(db =>
        {
            db.Set<GroupMember>().Add(new GroupMember { GroupId = id, TeamId = team.Id });
            return db.SaveChangesAsync();
        });

        (await ceo.PostAsync("/api/v1/admin/fantasy/winners", new { groupId = id, places = new[] { new { rank = 1, amount = 50m } } })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var winners = await ceo.PostAsync("/api/v1/admin/fantasy/winners", new { groupId = id, places = new[] { new { rank = 1, amount = 1000m }, new { rank = 2, amount = 500m } } });
        winners.Status.ShouldBe(HttpStatusCode.OK, winners.Body.ToString());
        winners.Body.Data().GetArrayLength().ShouldBe(1);
        var voucher = await api.DbAsync(db => db.Set<Voucher>().SingleAsync(v => v.UserId == userId));
        (voucher.Amount, voucher.Source, voucher.Status).ShouldBe((1000m, VoucherSource.FantasyPrize, VoucherStatus.Active));
        (await ceo.PostAsync("/api/v1/admin/fantasy/winners", new { groupId = id, places = new[] { new { rank = 1, amount = 1000m } } })).Body.Code().ShouldBe("winners_announced");
        (await player.PostAsync("/api/v1/admin/fantasy/winners", new { groupId = id, places = new[] { new { rank = 1, amount = 1000m } } })).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
