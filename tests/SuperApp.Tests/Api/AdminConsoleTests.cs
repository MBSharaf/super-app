using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Notifications;

namespace SuperApp.Tests.Api;

/// <summary>Admin console on the API (API §7.2): people, orders and bookings, audit, reports, settings, maintenance, broadcasts; CEO reports.</summary>
[Trait("Category", "Database")]
public sealed class AdminConsoleTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private async Task<(ApiClient Client, Guid Id)> CustomerAsync()
    {
        var client = await api.ClientAsync();
        var email = $"c{Guid.NewGuid():N}"[..12] + "@test.example";
        var signUp = await client.PostAsync("/api/v1/auth/sign-up", new { fullName = "Admin Test Customer", email, phone = "+2011" + Random.Shared.Next(10_000_000, 99_999_999), password = "Football2026", acceptTerms = true });
        signUp.Status.ShouldBe(HttpStatusCode.Created, signUp.Body.ToString());
        var id = await api.DbAsync(db => db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync());
        return (client, id);
    }

    [Fact]
    public async Task Customers_are_searched_opened_blocked_and_unblocked_and_the_customer_is_told()
    {
        var ceo = await api.CeoAsync();
        var (_, id) = await CustomerAsync();
        var list = await ceo.GetAsync("/api/v1/admin/customers?q=Admin%20Test%20Customer&pageSize=100");
        list.Status.ShouldBe(HttpStatusCode.OK, list.Body.ToString());
        list.Body.Data().EnumerateArray().ShouldContain(c => c.GetProperty("id").GetGuid() == id);

        (await ceo.PostAsync($"/api/v1/admin/customers/{id}/block", new { reason = "" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var blocked = await ceo.PostAsync($"/api/v1/admin/customers/{id}/block", new { reason = "Many no-shows" });
        blocked.Body.Data().GetProperty("customer").GetProperty("status").GetString().ShouldBe("BlockedFromBookingAndOrdering");
        (await ceo.PostAsync($"/api/v1/admin/customers/{id}/unblock")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/customers/{id}/unblock")).Body.Code().ShouldBe("not_blocked");
        (await ceo.PostAsync($"/api/v1/admin/customers/{id}/prepay", new { rule = "Always" })).Body.Data().GetProperty("customer").GetProperty("mustPrepay").GetString().ShouldBe("Always");
        (await ceo.PostAsync($"/api/v1/admin/customers/{id}/prepay", new { rule = "Sometimes" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync($"/api/v1/admin/customers/{id}/note", new { note = "VIP" })).Body.Data().GetProperty("adminNote").GetString().ShouldBe("VIP");
        (await ceo.GetAsync($"/api/v1/admin/customers/{Guid.NewGuid()}")).Status.ShouldBe(HttpStatusCode.NotFound);

        var told = await api.DbAsync(db => db.Set<Notification>().CountAsync(n => n.UserId == id));
        told.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task A_customer_cannot_open_the_admin_console()
    {
        var (customer, _) = await CustomerAsync();
        await customer.RefreshCsrfAsync();
        (await customer.GetAsync("/api/v1/admin/customers")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await customer.GetAsync("/api/v1/admin/overview")).Status.ShouldBe(HttpStatusCode.Forbidden);
        (await (await api.ClientAsync()).GetAsync("/api/v1/admin/audit")).Status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Partners_and_sellers_are_listed_suspended_and_reactivated()
    {
        var ceo = await api.CeoAsync();
        var partners = (await ceo.GetAsync("/api/v1/admin/partners")).Body.Data();
        partners.GetArrayLength().ShouldBeGreaterThan(0);
        var partnerId = partners[0].GetProperty("id").GetInt64();
        (await ceo.GetAsync($"/api/v1/admin/partners/{partnerId}")).Body.Data().GetProperty("venues").GetArrayLength().ShouldBeGreaterThan(0);
        (await ceo.PostAsync($"/api/v1/admin/partners/{partnerId}/suspend", new { reason = "Unpaid fees review" })).Body.Data().GetProperty("partner").GetProperty("status").GetString().ShouldBe("Suspended");
        (await ceo.PostAsync($"/api/v1/admin/partners/{partnerId}/suspend", new { reason = "Again" })).Body.Code().ShouldBe("same_status");
        (await ceo.PostAsync($"/api/v1/admin/partners/{partnerId}/reactivate")).Status.ShouldBe(HttpStatusCode.OK);

        var sellers = (await ceo.GetAsync("/api/v1/admin/sellers")).Body.Data();
        sellers.GetArrayLength().ShouldBeGreaterThan(0);
        var sellerId = sellers[0].GetProperty("id").GetInt64();
        (await ceo.GetAsync($"/api/v1/admin/sellers/{sellerId}")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/sellers/{sellerId}/feature", new { featured = true })).Body.Data().GetProperty("seller").GetProperty("featured").GetBoolean().ShouldBeTrue();
        (await ceo.PostAsync($"/api/v1/admin/sellers/{sellerId}/suspend", new { reason = "Copied art" })).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.PostAsync($"/api/v1/admin/sellers/{sellerId}/reactivate")).Status.ShouldBe(HttpStatusCode.OK);
        (await ceo.GetAsync("/api/v1/admin/sellers/999999")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Orders_bookings_overview_and_audit_are_readable_and_exported()
    {
        var ceo = await api.CeoAsync();
        (await ceo.GetAsync("/api/v1/admin/orders?pageSize=5")).Status.ShouldBe(HttpStatusCode.OK);
        var bookings = await ceo.GetAsync("/api/v1/admin/bookings?pageSize=5");
        bookings.Status.ShouldBe(HttpStatusCode.OK, bookings.Body.ToString());
        var overview = await ceo.GetAsync("/api/v1/admin/overview");
        overview.Status.ShouldBe(HttpStatusCode.OK, overview.Body.ToString());
        overview.Body.Data().GetProperty("partners").GetInt32().ShouldBeGreaterThan(0);

        await ceo.SendAsync(HttpMethod.Put, "/api/v1/ceo/switches/kill.games", new { enabled = true });
        var audit = await ceo.GetAsync("/api/v1/admin/audit?action=switch.");
        audit.Body.Data().GetArrayLength().ShouldBeGreaterThan(0);
        (await ceo.GetAsync("/api/v1/ceo/audit?system=Ceo")).Status.ShouldBe(HttpStatusCode.OK);

        var file = await ceo.Http.GetAsync("/api/v1/admin/audit/export.xlsx");
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        (await ceo.Http.GetAsync("/api/v1/admin/customers/export.xlsx")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("operations")]
    [InlineData("customers")]
    [InlineData("partners")]
    [InlineData("sellers")]
    [InlineData("support")]
    [InlineData("agents")]
    public async Task Every_non_money_report_is_an_excel_file(string kind)
    {
        var ceo = await api.CeoAsync();
        var file = await ceo.Http.GetAsync($"/api/v1/ceo/reports/{kind}?from=2026-01-01&to=2026-12-31");
        file.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await file.Content.ReadAsByteArrayAsync()).Length.ShouldBeGreaterThan(1000);
        (await ceo.Http.GetAsync($"/api/v1/admin/reports/{kind}.xlsx")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Money_reports_wait_and_bad_ranges_are_refused()
    {
        var ceo = await api.CeoAsync();
        var finance = await ceo.GetAsync("/api/v1/ceo/reports/finance");
        finance.Status.ShouldBe((HttpStatusCode)422);
        finance.Body.Code().ShouldBe("money_report_later");
        (await ceo.GetAsync("/api/v1/ceo/reports/nothing")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await ceo.GetAsync("/api/v1/ceo/reports/operations?from=2026-05-01&to=2026-01-01")).Status.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Non_money_settings_change_and_money_settings_are_refused()
    {
        var ceo = await api.CeoAsync();
        var all = (await ceo.GetAsync("/api/v1/admin/settings")).Body.Data().EnumerateArray().ToList();
        all.Single(s => s.GetProperty("key").GetString() == "commission.bookings").GetProperty("editable").GetBoolean().ShouldBeFalse();

        var money = await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/settings/money", new { values = new Dictionary<string, object> { ["commission.bookings"] = 0.5 } });
        money.Status.ShouldBe(HttpStatusCode.Forbidden);
        money.Body.Code().ShouldBe("money_setting");
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/settings/community", new { values = new Dictionary<string, object> { ["nope"] = 1 } })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/settings/nothing", new { values = new Dictionary<string, object> { ["x"] = 1 } })).Status.ShouldBe(HttpStatusCode.NotFound);

        var rules = new { value = new[] { "Be kind", "No spam" } };
        var saved = await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/settings/community", new { values = new Dictionary<string, object> { ["community.rules"] = rules } });
        saved.Status.ShouldBe(HttpStatusCode.OK, saved.Body.ToString());
        saved.Body.Data()[0].GetProperty("value").GetProperty("value").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Maintenance_pauses_bookings_and_the_shop_with_a_message()
    {
        var ceo = await api.CeoAsync();
        var on = await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/maintenance", new { pauseBookings = true, pauseShop = false, message = "Upgrading servers" });
        try
        {
            on.Status.ShouldBe(HttpStatusCode.OK, on.Body.ToString());
            on.Body.Data().GetProperty("bookingsPaused").GetBoolean().ShouldBeTrue();
            on.Body.Data().GetProperty("message").GetString().ShouldBe("Upgrading servers");
        }
        finally
        {
            var off = await ceo.SendAsync(HttpMethod.Put, "/api/v1/admin/maintenance", new { pauseBookings = false, pauseShop = false });
            off.Body.Data().GetProperty("bookingsPaused").GetBoolean().ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Broadcasts_reach_the_segment_now_or_are_scheduled()
    {
        var ceo = await api.CeoAsync();
        var now = await ceo.PostAsync("/api/v1/admin/broadcasts", new { title = "Ramadan hours", body = "Pitches open late.", link = "#/pitches", segment = "Partners", sendEmail = false });
        now.Status.ShouldBe(HttpStatusCode.Created, now.Body.ToString());
        now.Body.Data().GetProperty("recipients").GetInt32().ShouldBeGreaterThan(0);
        var later = await ceo.PostAsync("/api/v1/admin/broadcasts", new { title = "Later", body = "Soon", segment = "Customers", sendEmail = false, scheduledAt = DateTime.UtcNow.AddDays(1) });
        later.Status.ShouldBe(HttpStatusCode.Accepted);
        (await ceo.PostAsync("/api/v1/admin/broadcasts", new { title = "x", body = "y", segment = "Martians" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync("/api/v1/admin/broadcasts", new { title = "x", body = "y", segment = "Everyone", link = "https://evil.example" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.GetAsync("/api/v1/admin/broadcasts")).Body.Data().GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);
    }
}
