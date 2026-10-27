using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Shop;

namespace SuperApp.Tests.Api;

/// <summary>Courier updates, tracking, refusals, level-ups and admin parcel tools (API §4.7).</summary>
[Trait("Category", "Database")]
public sealed class ShippingTests(DemoApiFactory api) : IClassFixture<DemoApiFactory>
{
    private readonly ShopKit kit = new(api);
    private readonly PartnerKit partners = new(api);

    /// <summary>A store order (COD) that the partner packed and shipped; returns the order code and parcel id.</summary>
    private async Task<(ApiClient Customer, string Order, long Parcel)> ShippedStoreOrderAsync()
    {
        var (partner, _) = await partners.PersonAsync(partner: true);
        await partner.GetAsync("/api/v1/partner/store");
        var product = (await partner.PostAsync("/api/v1/partner/store/products", new
        {
            name = "Match Bibs", categoryKey = "training", price = 300m, lowStockAt = 1, status = "Published", specs = Array.Empty<object>(),
            variants = new[] { new { id = (long?)null, size = "One size", colorName = "Orange", colorHex = (string?)null, sku = (string?)null, stock = 20, priceOverride = (decimal?)null } },
        })).Body.Data();
        await partner.SendFormAsync(HttpMethod.Post, $"/api/v1/partner/store/products/{product.GetProperty("id").GetInt64()}/images", new { }, ("photos", "b.png", ShopKit.Png()));
        await kit.ApproveAsync(product.GetProperty("id").GetInt64());
        var (customer, _) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { productVariantId = product.GetProperty("variants")[0].GetProperty("id").GetInt64(), qty = 1 });
        var order = (await ShopKit.CheckoutAsync(customer, payment: "cod")).Body.Data().GetProperty("code").GetString()!;
        await partner.PostAsync($"/api/v1/partner/store/orders/{order}/pack");
        await partner.PostAsync($"/api/v1/partner/store/orders/{order}/ship");
        var parcel = await api.DbAsync(db => db.Set<Shipment>().Where(s => s.Order.Code == order).Select(s => s.Id).SingleAsync());
        return (customer, order, parcel);
    }

    /// <summary>A printed order whose job is packed by the demo factory, so the courier is booked.</summary>
    private async Task<(ApiClient Customer, string Order, Shipment Parcel)> PrintedParcelAsync(long designId, string payment = "online")
    {
        var (customer, _) = await kit.PersonAsync();
        await customer.PostAsync("/api/v1/cart/items", new { designId, size = "M", color = "Black", qty = 1 });
        var order = (await ShopKit.CheckoutAsync(customer, payment: payment)).Body.Data().GetProperty("code").GetString()!;
        var (owner, ownerId) = await kit.PersonAsync();
        await api.DbAsync(db => db.Set<Factory>().Where(f => f.Name == "Nile DTF Works").ExecuteUpdateAsync(u => u.SetProperty(f => f.OwnerUserId, ownerId)));
        var job = await api.DbAsync(db => db.Set<Job>().Where(j => j.OrderItem.Order.Code == order).Select(j => j.Code).SingleAsync());
        await api.DbAsync(db => db.Set<Job>().Where(j => j.Code == job).ExecuteUpdateAsync(u => u.SetProperty(j => j.Stage, JobStage.Packing)));
        (await owner.PostAsync($"/api/v1/factory/jobs/{job}/advance", new { toStage = "Ready" })).Status.ShouldBe(HttpStatusCode.OK);
        var parcel = await api.DbAsync(db => db.Set<Shipment>().SingleAsync(s => s.Order.Code == order));
        return (customer, order, parcel);
    }

    private async Task<(HttpStatusCode Status, bool Applied)> WebhookAsync(string tracking, string status, string? eventId, bool sign = true, string provider = "courier.simulated")
    {
        var body = $"{{\"trackingNumber\":\"{tracking}\",\"status\":\"{status}\",\"eventId\":{(eventId is null ? "null" : $"\"{eventId}\"")},\"description\":\"test\"}}";
        var courier = api.Services.GetRequiredService<SimulatedCourier>();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/webhooks/courier/{provider}") { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("X-Courier-Signature", sign ? courier.Sign(body) : "00");
        var response = await (await api.ClientAsync()).Http.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, response.IsSuccessStatusCode && System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("data").GetProperty("applied").GetBoolean());
    }

    [Fact]
    public async Task A_store_parcel_is_tracked_from_pickup_to_delivery()
    {
        var (customer, order, parcel) = await ShippedStoreOrderAsync();
        var ceo = await api.CeoAsync();
        var tracking = (await customer.GetAsync($"/api/v1/orders/{order}/tracking")).Body.Data();
        tracking.GetArrayLength().ShouldBe(1);
        tracking[0].GetProperty("status").GetString().ShouldBe("AwaitingPickup");
        foreach (var status in new[] { "PickedUp", "OutForDelivery", "Delivered" })
        {
            (await ceo.PostAsync($"/api/v1/admin/shipments/{parcel}/simulate", new { status })).Body.Data().GetProperty("status").GetString().ShouldBe(status);
        }
        (await customer.GetAsync($"/api/v1/orders/{order}")).Body.Data().GetProperty("status").GetString().ShouldBe("Completed");
        (await api.DbAsync(db => db.Set<Fulfilment>().Where(f => f.Order.Code == order).Select(f => f.Status).SingleAsync())).ShouldBe(FulfilmentStatus.Delivered);
        (await customer.GetAsync($"/api/v1/orders/{order}/tracking")).Body.Data()[0].GetProperty("events").GetArrayLength().ShouldBe(4);
        (await ceo.PostAsync($"/api/v1/admin/shipments/{parcel}/simulate", new { status = "Refused" })).Body.Data().GetProperty("status").GetString().ShouldBe("Delivered");
        var (other, _) = await kit.PersonAsync();
        (await other.GetAsync($"/api/v1/orders/{order}/tracking")).Status.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Signed_courier_webhooks_move_printed_items_count_sales_and_ignore_replays()
    {
        var design = await kit.DesignIdAsync("Retro 90");
        var sales = await api.DbAsync(db => db.Set<Design>().Where(d => d.Id == design).Select(d => d.Sales).SingleAsync());
        var (customer, order, parcel) = await PrintedParcelAsync(design);
        (await WebhookAsync(parcel.TrackingNumber, "PickedUp", "evt-1", sign: false)).Status.ShouldBe(HttpStatusCode.Unauthorized);
        (await WebhookAsync(parcel.TrackingNumber, "PickedUp", "evt-1", provider: "courier.nobody")).Status.ShouldBe(HttpStatusCode.NotFound);
        (await WebhookAsync(parcel.TrackingNumber, "PickedUp", $"evt-{parcel.Id}-1")).Applied.ShouldBeTrue();
        (await WebhookAsync(parcel.TrackingNumber, "PickedUp", $"evt-{parcel.Id}-1")).Applied.ShouldBeFalse();
        (await api.DbAsync(db => db.Set<Job>().Where(j => j.OrderItem.Order.Code == order).Select(j => j.Stage).SingleAsync())).ShouldBe(JobStage.Shipped);
        (await WebhookAsync(parcel.TrackingNumber, "Delivered", $"evt-{parcel.Id}-2")).Applied.ShouldBeTrue();
        (await customer.GetAsync($"/api/v1/orders/{order}")).Body.Data().GetProperty("groups")[0].GetProperty("status").GetString().ShouldBe("Delivered");
        (await api.DbAsync(db => db.Set<Design>().Where(d => d.Id == design).Select(d => d.Sales).SingleAsync())).ShouldBe(sales + 1);
    }

    [Fact]
    public async Task A_refused_cash_parcel_counts_against_the_customer_and_sellers_level_up_on_delivery()
    {
        var design = await kit.DesignIdAsync("Ya Habibi");
        var (_, order, parcel) = await PrintedParcelAsync(design, "cod");
        var customer = await api.DbAsync(db => db.Set<Order>().Where(o => o.Code == order).Select(o => o.CustomerId).SingleAsync());
        (await WebhookAsync(parcel.TrackingNumber, "Refused", null)).Applied.ShouldBeTrue();
        (await api.DbAsync(db => db.Users.Where(u => u.Id == customer).Select(u => u.CodRefusals).SingleAsync())).ShouldBe(1);
        (await api.DbAsync(db => db.Set<OrderItem>().Where(i => i.Order.Code == order).Select(i => i.Status).SingleAsync())).ShouldBe(OrderItemStatus.Refused);

        var (_, sellerUser, sellerId, _) = await kit.SellerAsync();
        await api.DbAsync(db => db.Set<Seller>().Where(s => s.Id == sellerId).ExecuteUpdateAsync(u => u.SetProperty(s => s.LifetimeSales, 9)));
        var mine = await kit.DesignAsync(sellerId, DesignStatus.Live, "Level Up Tee");
        var (_, _, delivered) = await PrintedParcelAsync(mine);
        await WebhookAsync(delivered.TrackingNumber, "Delivered", null);
        (await api.DbAsync(db => db.Set<Seller>().Where(s => s.Id == sellerId).Select(s => s.Level.Key).SingleAsync())).ShouldBe("rising");
        (await api.DbAsync(db => db.Set<SuperApp.Domain.Entities.Notifications.Notification>().AnyAsync(n => n.UserId == sellerUser && n.Title.Contains("Rising")))).ShouldBeTrue();
    }

    [Fact]
    public async Task Admins_cancel_rebook_and_only_simulate_simulated_parcels()
    {
        var (_, order, parcel) = await ShippedStoreOrderAsync();
        var ceo = await api.CeoAsync();
        (await ceo.GetAsync("/api/v1/admin/shipments?provider=courier.simulated")).Body.Data().GetArrayLength().ShouldBeGreaterThan(0);
        (await ceo.PostAsync($"/api/v1/admin/shipments/{parcel}/cancel")).Body.Data().GetProperty("status").GetString().ShouldBe("Cancelled");
        (await api.DbAsync(db => db.Set<Fulfilment>().Where(f => f.Order.Code == order).Select(f => f.Status).SingleAsync())).ShouldBe(FulfilmentStatus.Packed);
        var fresh = (await ceo.PostAsync($"/api/v1/admin/shipments/{parcel}/rebook")).Body.Data();
        fresh.GetProperty("status").GetString().ShouldBe("AwaitingPickup");
        var freshId = fresh.GetProperty("id").GetInt64();
        await ceo.PostAsync($"/api/v1/admin/shipments/{freshId}/simulate", new { status = "PickedUp" });
        (await ceo.PostAsync($"/api/v1/admin/shipments/{freshId}/cancel")).Body.Code().ShouldBe("wrong_state");
        await api.DbAsync(db => db.Set<Shipment>().Where(s => s.Id == freshId).ExecuteUpdateAsync(u => u.SetProperty(s => s.Mode, IntegrationMode.Test)));
        (await ceo.PostAsync($"/api/v1/admin/shipments/{freshId}/simulate", new { status = "Delivered" })).Body.Code().ShouldBe("not_simulated");
        var (stranger, _) = await kit.PersonAsync();
        (await stranger.GetAsync("/api/v1/admin/shipments")).Status.ShouldBe(HttpStatusCode.Forbidden);
    }
}
