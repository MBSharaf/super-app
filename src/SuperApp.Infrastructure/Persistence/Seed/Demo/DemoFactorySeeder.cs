using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Shop;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Demo 20 (Phase 4): the print factory of today's site — "Nile DTF Works" owned by the factory owner, its six staff with
/// the PINs the demo site shows (fictional), its price per product, and one production job for every printed item of
/// the demo orders (stage from the item's status, deadline 48 hours after the order).
/// </summary>
public sealed class DemoFactorySeeder(AppDbContext db) : ISeeder
{
    private const string Name = "Nile DTF Works";

    private static readonly (string Name, FactoryRole Role, string Pin)[] Staff =
    [
        ("Hany Mostafa", FactoryRole.Owner, "1111"), ("Reem Adel", FactoryRole.Supervisor, "2222"), ("Mahmoud Samir", FactoryRole.Printer, "3333"),
        ("Omar Fawzy", FactoryRole.Printer, "3344"), ("Nour Hassan", FactoryRole.QualityCheck, "4444"), ("Islam Gamal", FactoryRole.Packer, "5555"),
    ];

    public int Order => 20;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var users = await DemoRefs.UsersAsync(db, ct);
        if (!users.TryGetValue("acc-print", out var owner) || await db.Set<Factory>().IgnoreQueryFilters().AnyAsync(f => f.Name == Name, ct))
        {
            return 0;
        }
        var area = await db.Set<Area>().OrderBy(a => a.NameEn.Contains("Obour") ? 0 : 1).ThenBy(a => a.Id).Select(a => a.Id).FirstAsync(ct);
        var factory = new Factory
        {
            OwnerUserId = owner, Name = Name, Address = "Industrial Zone B, plot 41, Obour City", AreaId = area, CapacityPerDay = 150, SlaHours = 48,
            Techniques = "DTF", Status = FactoryStatus.Active, PayoutDetail = "nile-dtf@instapay",
        };
        db.Set<Factory>().Add(factory);
        db.Set<FactoryStaffMember>().AddRange(Staff.Select(s => new FactoryStaffMember { Factory = factory, Name = s.Name, Role = s.Role, PinHash = PinHasher.Hash(s.Pin) }));
        var catalogue = await db.Set<CatalogProduct>().ToListAsync(ct);
        db.Set<FactoryPrice>().AddRange(catalogue.Select(c => new FactoryPrice { Factory = factory, CatalogProductId = c.Id, PricePerPiece = c.FactoryCost, BackPrintPrice = 40 }));
        await db.SaveChangesAsync(ct);
        return 1 + Staff.Length + catalogue.Count + await AddJobsAsync(factory.Id, ct);
    }

    private async Task<int> AddJobsAsync(long factoryId, CancellationToken ct)
    {
        var items = await db.Set<OrderItem>().Where(i => i.Kind == OrderItemKind.PrintedDesign && !db.Set<Job>().Any(j => j.OrderItemId == i.Id))
            .Select(i => new { i.Id, i.Status, i.CreatedAt }).ToListAsync(ct);
        var n = 0;
        foreach (var item in items)
        {
            db.Set<Job>().Add(new Job
            {
                Code = $"JB-{100000 + item.Id}", OrderItemId = item.Id, FactoryId = factoryId, Stage = StageOf(item.Status), DueAt = item.CreatedAt.AddHours(48),
            });
            n++;
        }
        await db.SaveChangesAsync(ct);
        return n;
    }

    private static JobStage StageOf(OrderItemStatus status) => status switch
    {
        OrderItemStatus.Received => JobStage.New,
        OrderItemStatus.Printing => JobStage.Printing,
        OrderItemStatus.Packed => JobStage.Ready,
        OrderItemStatus.Cancelled => JobStage.Cancelled,
        _ => JobStage.Shipped,
    };
}
