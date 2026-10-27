using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using Shouldly;
using SuperApp.Infrastructure.Shop;

namespace SuperApp.Tests.Api;

/// <summary>Test helpers for Phase 4: sellers (as after an approved application), designs, factories and a tiny PNG.</summary>
public sealed class ShopKit(AccountsApiFactory api)
{
    private readonly PartnerKit people = new(api);

    /// <summary>A transparent 600أ—600 PNG (enough pixels for a dpi check).</summary>
    public static byte[] Png(int size = 600)
    {
        using var bitmap = new SkiaSharp.SKBitmap(size, size);
        bitmap.Erase(new SkiaSharp.SKColor(200, 30, 30, 128));
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public Task<(ApiClient Client, Guid UserId)> PersonAsync() => people.PersonAsync(partner: false);

    /// <summary>A signed-in seller with a store at the given level ("new" = Starter, "rising", "pro", "elite").</summary>
    public async Task<(ApiClient Client, Guid UserId, long SellerId, string Handle)> SellerAsync(string level = "new", SellerMode mode = SellerMode.Both)
    {
        var (client, userId) = await people.PersonAsync(partner: false);
        var handle = "t-" + Guid.NewGuid().ToString("N")[..10];
        var sellerId = await api.DbAsync(async db =>
        {
            var store = new Store { OwnerUserId = userId, Type = StoreType.Seller, Handle = handle, Name = "Test Prints " + handle[2..6], Status = StoreStatus.Active };
            var levelId = await db.Set<SellerLevel>().Where(l => l.Key == level).Select(l => l.Id).SingleAsync();
            var seller = new Seller { UserId = userId, Store = store, LevelId = levelId, Mode = mode, Status = SellerStatus.Active, PayoutMethod = PayoutMethod.InstaPay, PayoutDetail = "test@instapay" };
            db.Set<Seller>().Add(seller);
            await db.SaveChangesAsync();
            return seller.Id;
        });
        return (client, userId, sellerId, handle);
    }

    /// <summary>A design of the seller straight in the database (status as asked), on the classic tee in Black and White.</summary>
    public Task<long> DesignAsync(long sellerId, DesignStatus status, string title = "Test Design", decimal price = 450) => api.DbAsync(async db =>
    {
        var tee = await db.Set<CatalogProduct>().Where(c => c.Key == "pod-tee").Select(c => c.Id).SingleAsync();
        var category = await db.Set<DesignCategory>().Where(c => c.Key == "football").Select(c => c.Id).SingleAsync();
        var art = await db.Set<SuperApp.Domain.Entities.Ops.StoredFile>().Where(f => f.Container == "demo-art").Select(f => (long?)f.Id).FirstOrDefaultAsync();
        var design = new Design
        {
            SellerId = sellerId, CatalogProductId = tee, CategoryId = category, Title = title, Price = price, Status = status,
            FrontArtFileId = art, FrontX = art is null ? null : 0.5m, FrontY = art is null ? null : 0.4m, FrontW = art is null ? null : 0.4m,
        };
        db.Set<Design>().Add(design);
        await db.SaveChangesAsync();
        var colors = await db.Set<CatalogColor>().Where(c => c.CatalogProductId == tee && (c.NameEn == "Black" || c.NameEn == "White")).Select(c => c.Id).ToListAsync();
        db.Set<DesignColor>().AddRange(colors.Select(c => new DesignColor { DesignId = design.Id, CatalogColorId = c }));
        db.Set<DesignTag>().Add(new DesignTag { DesignId = design.Id, Tag = "test" });
        await db.SaveChangesAsync();
        return design.Id;
    });

    /// <summary>Makes sure a factory takes work (the reference seed has none; the demo seed has Nile DTF Works).</summary>
    public Task<long> FactoryAsync() => api.DbAsync(async db =>
    {
        var existing = await db.Set<Factory>().Where(f => f.Status == FactoryStatus.Active && !f.IntakePaused).Select(f => (long?)f.Id).FirstOrDefaultAsync();
        if (existing is { } id)
        {
            return id;
        }
        var (_, owner) = await people.PersonAsync(partner: false);
        var area = await db.Set<SuperApp.Domain.Entities.Geo.Area>().OrderBy(a => a.Id).Select(a => a.Id).FirstAsync();
        var factory = new Factory { OwnerUserId = owner, Name = "Test Print " + Guid.NewGuid().ToString("N")[..6], Address = "1 Test Zone", AreaId = area, CapacityPerDay = 100, PayoutDetail = "x" };
        db.Set<Factory>().Add(factory);
        db.Set<FactoryStaffMember>().Add(new FactoryStaffMember { Factory = factory, Name = "Test Printer", Role = FactoryRole.Printer, PinHash = PinHasher.Hash("1234") });
        await db.SaveChangesAsync();
        return factory.Id;
    });

    /// <summary>POST /orders/checkout with an Idempotency-Key header (a new key unless one is given).</summary>
    public static async Task<(System.Net.HttpStatusCode Status, System.Text.Json.JsonElement Body)> CheckoutAsync(ApiClient client, string payment = "online",
        string? promo = null, string? key = null, string speed = "standard")
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders/checkout")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                address = new { label = "Home", line1 = "12 Test Street, Dokki", line2 = "Floor 3", city = "Giza" },
                phone = "01012345678", deliverySpeed = speed, paymentMethod = payment, promoCode = promo,
            }),
        };
        if (key != "")
        {
            request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        }
        var response = await client.Http.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, string.IsNullOrEmpty(text) ? default : System.Text.Json.JsonDocument.Parse(text).RootElement.Clone());
    }

    /// <summary>The variant ids of a demo product, with their stock.</summary>
    public Task<List<(long Id, int Stock)>> VariantsAsync(string slug) => api.DbAsync(async db =>
        (await db.Set<ProductVariant>().Where(v => v.Product.Slug == slug).OrderBy(v => v.Id).Select(v => new { v.Id, v.Stock }).ToListAsync())
            .Select(v => (v.Id, v.Stock)).ToList());

    /// <summary>Super App approves a product a store asked to sell (it goes on sale).</summary>
    public async Task ApproveAsync(long productId)
    {
        var approved = await (await api.CeoAsync()).PostAsync($"/api/v1/admin/products/{productId}/approve");
        approved.Status.ShouldBe(System.Net.HttpStatusCode.OK, approved.Body.ToString());
    }

    public Task<long> DesignIdAsync(string title) => api.DbAsync(db => db.Set<Design>().Where(d => d.Title == title).Select(d => d.Id).SingleAsync());
}
