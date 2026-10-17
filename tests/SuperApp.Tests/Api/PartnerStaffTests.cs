using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Notifications;

namespace SuperApp.Tests.Api;

/// <summary>The partner's staff accounts (API §3.2): add by login, roles, pause, remove, and who may do it.</summary>
[Trait("Category", "Database")]
public sealed class PartnerStaffTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private readonly PartnerKit kit = new(api);

    private Task<string> EmailAsync(Guid userId) => api.DbAsync(db => db.Users.Where(u => u.Id == userId).Select(u => u.Email!).SingleAsync());

    [Fact]
    public async Task The_owner_adds_staff_by_email_and_they_get_the_dashboard()
    {
        var (owner, _, _, _) = await kit.PartnerWithPitchAsync();
        var (desk, deskId) = await kit.PersonAsync(partner: false);
        var email = await EmailAsync(deskId);

        var added = await owner.PostAsync("/api/v1/partner/staff", new { login = email, role = "CheckIn" });
        added.Status.ShouldBe(HttpStatusCode.Created, added.Body.ToString());
        added.Body.Data().GetProperty("role").GetString().ShouldBe("CheckIn");
        (await desk.GetAsync("/api/v1/partner/pitches")).Body.Data().GetArrayLength().ShouldBe(1);
        (await api.DbAsync(db => db.Set<Notification>().AnyAsync(n => n.UserId == deskId && n.Title == "You joined Test Arena Co as check-in staff"))).ShouldBeTrue();

        (await owner.PostAsync("/api/v1/partner/staff", new { login = email, role = "CheckIn" })).Body.Code().ShouldBe("already_staff");
        (await owner.PostAsync("/api/v1/partner/staff", new { login = "nobody@test.example", role = "CheckIn" })).Body.Code().ShouldBe("no_account");
        (await owner.PostAsync("/api/v1/partner/staff", new { login = email, role = "Boss" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await desk.GetAsync("/api/v1/partner/staff")).Body.Code().ShouldBe("manager_only");
    }

    [Fact]
    public async Task Managers_handle_other_staff_but_only_the_owner_handles_managers()
    {
        var (owner, _, _, _) = await kit.PartnerWithPitchAsync();
        var (manager, managerId) = await kit.PersonAsync(partner: false);
        var managerRow = (await owner.PostAsync("/api/v1/partner/staff", new { login = await EmailAsync(managerId), role = "Manager" })).Body.Data().GetProperty("id").GetInt64();
        var (accountant, accountantId) = await kit.PersonAsync(partner: false);
        var (other, otherId) = await kit.PersonAsync(partner: false);

        var byManager = await manager.PostAsync("/api/v1/partner/staff", new { login = await EmailAsync(accountantId), role = "Accountant" });
        byManager.Status.ShouldBe(HttpStatusCode.Created, byManager.Body.ToString());
        (await manager.PostAsync("/api/v1/partner/staff", new { login = await EmailAsync(otherId), role = "Manager" })).Body.Code().ShouldBe("owner_only");
        (await manager.SendAsync(HttpMethod.Delete, $"/api/v1/partner/staff/{managerRow}")).Body.Code().ShouldBe("owner_only");

        var accountantRow = byManager.Body.Data().GetProperty("id").GetInt64();
        (await owner.SendAsync(HttpMethod.Put, $"/api/v1/partner/staff/{accountantRow}", new { role = "Accountant", isActive = false })).Status.ShouldBe(HttpStatusCode.OK);
        (await accountant.GetAsync("/api/v1/partner/bookings")).Body.Code().ShouldBe("not_partner");
        (await owner.SendAsync(HttpMethod.Delete, $"/api/v1/partner/staff/{accountantRow}")).Status.ShouldBe(HttpStatusCode.OK);
        (await owner.GetAsync("/api/v1/partner/staff")).Body.Data().GetArrayLength().ShouldBe(1);
        (await other.GetAsync("/api/v1/partner/staff")).Body.Code().ShouldBe("not_partner");
    }
}
