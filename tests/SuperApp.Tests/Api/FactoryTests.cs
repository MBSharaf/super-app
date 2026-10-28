using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>The factory system (API §4.6) and the admin production view, on the demo factory "Nile DTF Works".</summary>
[Trait("Category", "Database")]
public sealed class FactoryTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private readonly ShopKit kit = new(api);

    /// <summary>A signed-in person who owns the demo factory, with a registered device.</summary>
    private async Task<(ApiClient Owner, string Device)> OwnerAsync()
    {
        var (owner, userId) = await kit.PersonAsync();
        await api.DbAsync(db => db.Set<Factory>().Where(f => f.Name == "Nile DTF Works")
            .ExecuteUpdateAsync(u => u.SetProperty(f => f.OwnerUserId, userId).SetProperty(f => f.Status, FactoryStatus.Active).SetProperty(f => f.IntakePaused, false)));
        var device = await owner.PostAsync("/api/v1/factory/devices/register", new { name = "Packing tablet" });
        device.Status.ShouldBe(HttpStatusCode.Created, device.Body.ToString());
        return (owner, device.Body.Data().GetProperty("token").GetString()!);
    }

    private async Task<ApiClient> StaffAsync(string device, string name, string pin)
    {
        var client = await api.ClientAsync();
        client.Http.DefaultRequestHeaders.Add("X-Factory-Device", device);
        var staff = (await client.GetAsync("/api/v1/factory/staff/list")).Body.Data().EnumerateArray().Single(s => s.GetProperty("name").GetString() == name);
        var session = await client.PostAsync("/api/v1/factory/auth/pin", new { staffId = staff.GetProperty("id").GetInt64(), pin });
        session.Status.ShouldBe(HttpStatusCode.OK, session.Body.ToString());
        client.Http.DefaultRequestHeaders.Add("X-Factory-Session", session.Body.Data().GetProperty("token").GetString());
        return client;
    }

    private async Task<(string Order, string Job)> PrintedOrderAsync(string design = "Keep Calm & Press High")
    {
        var (customer, _) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { designId = await kit.DesignIdAsync(design), size = "L", color = "Black", qty = 1 });
        var order = (await ShopKit.CheckoutAsync(customer)).Body.Data().GetProperty("code").GetString()!;
        var job = await api.DbAsync(db => db.Set<Job>().Where(j => j.OrderItem.Order.Code == order).Select(j => j.Code).SingleAsync());
        return (order, job);
    }

    [Fact]
    public async Task Staff_sign_in_with_a_pin_on_a_registered_device()
    {
        var (owner, device) = await OwnerAsync();
        var anon = await api.ClientAsync();
        (await anon.GetAsync("/api/v1/factory/staff/list")).Status.ShouldBe(HttpStatusCode.Unauthorized);
        anon.Http.DefaultRequestHeaders.Add("X-Factory-Device", device);
        var staff = (await anon.GetAsync("/api/v1/factory/staff/list")).Body.Data();
        staff.GetArrayLength().ShouldBeGreaterThanOrEqualTo(6);
        var qc = staff.EnumerateArray().Single(s => s.GetProperty("role").GetString() == "QualityCheck").GetProperty("id").GetInt64();
        (await anon.PostAsync("/api/v1/factory/auth/pin", new { staffId = qc, pin = "0000" })).Body.Code().ShouldBe("bad_pin");
        for (var i = 0; i < 4; i++) { await anon.PostAsync("/api/v1/factory/auth/pin", new { staffId = qc, pin = "0000" }); }
        (await anon.PostAsync("/api/v1/factory/auth/pin", new { staffId = qc, pin = "4444" })).Status.ShouldBe((HttpStatusCode)423);
        (await anon.PostAsync("/api/v1/factory/auth/pin", new { staffId = qc, pin = "12a4" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await owner.PostAsync($"/api/v1/factory/team/{qc}/reset-pin", new { pin = "4444" })).Status.ShouldBe(HttpStatusCode.OK);

        var printer = await StaffAsync(device, "Mahmoud Samir", "3333");
        var me = (await printer.GetAsync("/api/v1/factory/me")).Body.Data();
        me.GetProperty("role").GetString().ShouldBe("Printer");
        me.GetProperty("can").EnumerateArray().Select(c => c.GetString()).ShouldBe(["print"]);
        printer.Http.DefaultRequestHeaders.Remove("X-Factory-Device");
        (await printer.GetAsync("/api/v1/factory/me")).Body.Code().ShouldBe("factory_device");
        var (stranger, _) = await kit.PersonAsync();
        (await stranger.GetAsync("/api/v1/factory/me")).Body.Code().ShouldBe("not_factory");
    }

    [Fact]
    public async Task Jobs_move_through_printing_quality_check_and_packing_then_the_courier_is_booked()
    {
        var (owner, device) = await OwnerAsync();
        var (order, job) = await PrintedOrderAsync();
        var printer = await StaffAsync(device, "Mahmoud Samir", "3333");
        var qc = await StaffAsync(device, "Nour Hassan", "4444");
        var packer = await StaffAsync(device, "Islam Gamal", "5555");

        (await printer.GetAsync("/api/v1/factory/jobs?pageSize=200")).Body.Data().EnumerateArray().ShouldContain(j => j.GetProperty("code").GetString() == job);
        var started = (await printer.PostAsync($"/api/v1/factory/jobs/{job}/advance", new { toStage = "Printing" })).Body.Data();
        started.GetProperty("card").GetProperty("assignedTo").GetString().ShouldBe("Mahmoud Samir");
        (await printer.PostAsync($"/api/v1/factory/jobs/{job}/advance", new { toStage = "Ready" })).Body.Code().ShouldBe("wrong_stage");
        await printer.PostAsync($"/api/v1/factory/jobs/{job}/advance", new { toStage = "QualityCheck" });
        (await printer.PostAsync($"/api/v1/factory/jobs/{job}/qc", new { pass = true })).Body.Code().ShouldBe("role");
        (await qc.PostAsync($"/api/v1/factory/jobs/{job}/qc", new { pass = false })).Status.ShouldBe(HttpStatusCode.BadRequest);
        var failed = (await qc.PostAsync($"/api/v1/factory/jobs/{job}/qc", new { pass = false, reason = "Cracked print" })).Body.Data();
        failed.GetProperty("card").GetProperty("stage").GetString().ShouldBe("Printing");
        failed.GetProperty("card").GetProperty("reprintCount").GetInt32().ShouldBe(1);
        await printer.PostAsync($"/api/v1/factory/jobs/{job}/advance", new { toStage = "QualityCheck" });
        await qc.PostAsync($"/api/v1/factory/jobs/{job}/qc", new { pass = true });
        var ready = (await packer.PostAsync($"/api/v1/factory/jobs/{job}/advance", new { toStage = "Ready" })).Body.Data();
        ready.GetProperty("events").GetArrayLength().ShouldBe(6);

        var parcels = (await packer.GetAsync("/api/v1/factory/shipping")).Body.Data();
        var parcel = parcels.EnumerateArray().Single(p => p.GetProperty("orderCode").GetString() == order);
        parcel.GetProperty("trackingNumber").GetString()!.ShouldStartWith("SIM");
        parcel.GetProperty("codAmount").GetDecimal().ShouldBe(0);   // paid online: the courier collects nothing
        var label = await packer.Http.GetAsync($"/api/v1/factory/shipments/{parcel.GetProperty("id").GetInt64()}/label.pdf");
        (await label.Content.ReadAsByteArrayAsync())[..4].ShouldBe("%PDF"u8.ToArray());
        (await printer.GetAsync("/api/v1/factory/shipping")).Body.Code().ShouldBe("role");
        var ticket = await owner.Http.GetAsync($"/api/v1/factory/jobs/{job}/ticket.pdf");
        ticket.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.PostAsync("/api/v1/factory/scan", new { code = $"https://superapp.example/job/{job.ToLowerInvariant()}" })).Body.Data().GetProperty("card").GetProperty("code").GetString().ShouldBe(job);
        (await api.DbAsync(db => db.Set<OrderItem>().Where(i => i.Order.Code == order).Select(i => i.Status).SingleAsync())).ShouldBe(OrderItemStatus.Packed);
    }

    [Fact]
    public async Task The_owner_runs_the_team_settings_money_and_reports()
    {
        var (owner, device) = await OwnerAsync();
        (await owner.PostAsync("/api/v1/factory/team", new { name = "New Owner", role = "Owner", pin = "1234" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await owner.PostAsync("/api/v1/factory/team", new { name = "Reem Adel", role = "Packer", pin = "1234" })).Body.Code().ShouldBe("name_taken");
        var added = (await owner.PostAsync("/api/v1/factory/team", new { name = "Sara Packer", role = "Packer", pin = "6666" })).Body.Data();
        (await owner.PostAsync($"/api/v1/factory/team/{added.GetProperty("id").GetInt64()}/reset-pin", new { pin = "7777" })).Status.ShouldBe(HttpStatusCode.OK);
        await StaffAsync(device, "Sara Packer", "7777");
        var supervisor = await StaffAsync(device, "Reem Adel", "2222");
        (await supervisor.GetAsync("/api/v1/factory/team")).Body.Code().ShouldBe("role");
        (await supervisor.GetAsync("/api/v1/factory/money")).Body.Code().ShouldBe("role");

        var paused = (await supervisor.SendAsync(HttpMethod.Put, "/api/v1/factory/settings", new { capacityPerDay = 1, slaHours = 1, intakePaused = true })).Body.Data();
        paused.GetProperty("intakePaused").GetBoolean().ShouldBeTrue();
        paused.GetProperty("capacityPerDay").GetInt32().ShouldBe(150);   // only the owner changes capacity
        (await (await api.ClientAsync()).GetAsync("/api/v1/shop/products?kind=design")).Body.Data().GetArrayLength().ShouldBe(0);
        (await owner.SendAsync(HttpMethod.Put, "/api/v1/factory/settings", new { capacityPerDay = 200, slaHours = 36, intakePaused = false })).Body.Data().GetProperty("slaHours").GetInt32().ShouldBe(36);

        (await owner.GetAsync("/api/v1/factory/money")).Body.Data().GetProperty("earned").GetDecimal().ShouldBeGreaterThan(0);   // demo jobs already shipped
        var xlsx = await owner.Http.GetAsync("/api/v1/factory/money/statement.xlsx");
        xlsx.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await supervisor.GetAsync("/api/v1/factory/reports")).Status.ShouldBe(HttpStatusCode.OK);
        (await owner.GetAsync("/api/v1/factory/overview")).Body.Data().GetProperty("capacityPerDay").GetInt32().ShouldBe(200);

        var (_, job) = await PrintedOrderAsync("Giza Run Club");
        var withProblem = (await supervisor.PostAsync($"/api/v1/factory/jobs/{job}/problem", new { kind = "MachineDown", detail = "Printer 2 is down" })).Body.Data();
        withProblem.GetProperty("problems").GetArrayLength().ShouldBe(1);
        var printerId = (await owner.GetAsync("/api/v1/factory/team")).Body.Data().EnumerateArray().First(s => s.GetProperty("role").GetString() == "Printer").GetProperty("id").GetInt64();
        (await supervisor.PostAsync($"/api/v1/factory/jobs/{job}/assign", new { staffId = printerId })).Body.Data().GetProperty("card").GetProperty("assignedTo").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Catalogue_changes_wait_for_an_admin_and_reach_sellers_when_approved()
    {
        var (owner, _) = await OwnerAsync();
        var tee = await api.DbAsync(db => db.Set<CatalogProduct>().Where(c => c.Key == "pod-tee").Select(c => new { c.Id, Color = c.Colors.First(x => x.NameEn == "Sand").Id }).SingleAsync());
        (await owner.PostAsync("/api/v1/factory/catalogue-changes", new { catalogProductId = tee.Id, kind = "Price", change = new { pricePerPiece = -5 }, reason = "Cotton prices" }))
            .Body.Code().ShouldBe("bad_change");
        var price = await owner.PostAsync("/api/v1/factory/catalogue-changes", new { catalogProductId = tee.Id, kind = "Price", change = new { pricePerPiece = 220 }, reason = "Cotton prices" });
        price.Status.ShouldBe(HttpStatusCode.Created, price.Body.ToString());
        (await owner.PostAsync("/api/v1/factory/catalogue-changes", new { catalogProductId = tee.Id, kind = "Price", change = new { pricePerPiece = 225 }, reason = "Again" }))
            .Body.Code().ShouldBe("pending_change");
        var colour = (await owner.PostAsync("/api/v1/factory/catalogue-changes", new { catalogProductId = tee.Id, kind = "Colour", change = new { colorId = tee.Color, active = false }, reason = "Out of sand blanks" })).Body.Data();

        var ceo = await api.CeoAsync();
        (await ceo.GetAsync("/api/v1/admin/catalogue-changes")).Body.Data().GetArrayLength().ShouldBeGreaterThanOrEqualTo(2);
        (await ceo.PostAsync($"/api/v1/admin/catalogue-changes/{price.Body.Data().GetProperty("id").GetInt64()}/approve")).Status.ShouldBe(HttpStatusCode.OK);
        (await api.DbAsync(db => db.Set<CatalogProduct>().Where(c => c.Id == tee.Id).Select(c => c.FactoryCost).SingleAsync())).ShouldBe(220);
        (await ceo.PostAsync($"/api/v1/admin/catalogue-changes/{colour.GetProperty("id").GetInt64()}/reject", new { resolution = "no" })).Status.ShouldBe(HttpStatusCode.BadRequest);
        (await ceo.PostAsync($"/api/v1/admin/catalogue-changes/{colour.GetProperty("id").GetInt64()}/approve")).Status.ShouldBe(HttpStatusCode.OK);
        (await api.DbAsync(db => db.Set<CatalogColor>().Where(c => c.Id == tee.Color).Select(c => c.IsActive).SingleAsync())).ShouldBeFalse();
        (await ceo.PostAsync($"/api/v1/admin/catalogue-changes/{colour.GetProperty("id").GetInt64()}/approve")).Body.Code().ShouldBe("wrong_status");
        (await owner.GetAsync("/api/v1/factory/catalogue-changes")).Body.Data().EnumerateArray().ShouldContain(c => c.GetProperty("status").GetString() == "Approved");
        await api.DbAsync(db => db.Set<CatalogColor>().Where(c => c.Id == tee.Color).ExecuteUpdateAsync(u => u.SetProperty(c => c.IsActive, true)));
    }

    [Fact]
    public async Task Admins_reassign_cancel_reprint_and_suspend()
    {
        var (owner, _) = await OwnerAsync();
        var ceo = await api.CeoAsync();
        (await ceo.GetAsync("/api/v1/admin/factories")).Body.Data().GetArrayLength().ShouldBeGreaterThanOrEqualTo(1);
        var (order, job) = await PrintedOrderAsync("Cairo Otaku");
        (await ceo.GetAsync("/api/v1/admin/jobs?pageSize=200")).Body.Data().EnumerateArray().ShouldContain(j => j.GetProperty("code").GetString() == job);

        var other = await api.DbAsync(async db =>
        {
            var f = new Factory { OwnerUserId = (await db.Users.FirstAsync()).Id, Name = "Second Print " + Guid.NewGuid().ToString("N")[..5], Address = "x", AreaId = 1, CapacityPerDay = 10, PayoutDetail = "x", IntakePaused = true };
            db.Set<Factory>().Add(f);
            await db.SaveChangesAsync();
            return f.Id;
        });
        (await ceo.PostAsync($"/api/v1/admin/jobs/{job}/reassign", new { factoryId = other })).Status.ShouldBe(HttpStatusCode.OK);
        (await owner.GetAsync($"/api/v1/factory/jobs/{job}")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await ceo.PostAsync($"/api/v1/admin/jobs/{job}/reprint", new { reason = "Customer complaint", deduction = 50m })).Status.ShouldBe(HttpStatusCode.OK);
        (await api.DbAsync(db => db.Set<Job>().Where(j => j.Code == job).Select(j => j.DeductionAmount).SingleAsync())).ShouldBe(50);
        (await ceo.PostAsync($"/api/v1/admin/jobs/{job}/cancel", new { resolution = "Out of stock everywhere" })).Status.ShouldBe(HttpStatusCode.OK);
        (await api.DbAsync(db => db.Set<Order>().Where(o => o.Code == order).Select(o => o.Status).SingleAsync())).ShouldBe(OrderStatus.Cancelled);
        (await ceo.PostAsync($"/api/v1/admin/jobs/{job}/cancel", new { resolution = "Again please" })).Body.Code().ShouldBe("wrong_stage");

        var demo = await api.DbAsync(db => db.Set<Factory>().Where(f => f.Name == "Nile DTF Works").Select(f => f.Id).SingleAsync());
        (await ceo.PostAsync($"/api/v1/admin/factories/{demo}/status", new { status = "Suspended" })).Status.ShouldBe(HttpStatusCode.OK);
        (await owner.GetAsync("/api/v1/factory/overview")).Body.Code().ShouldBe("factory_suspended");
        (await ceo.PostAsync($"/api/v1/admin/factories/{demo}/status", new { status = "Active" })).Status.ShouldBe(HttpStatusCode.OK);
        (await owner.PostAsync($"/api/v1/admin/factories/{demo}/status", new { status = "Active" })).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
