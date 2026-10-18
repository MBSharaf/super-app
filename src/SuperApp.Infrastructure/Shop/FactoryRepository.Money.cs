using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Factory;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Factory shipping list, money and reports.</summary>
public sealed partial class FactoryRepository
{
    public async Task<IReadOnlyList<FactoryShipmentDto>> ShipmentsAsync(long factoryId, string? status, CancellationToken ct)
    {
        var q = db.Set<Shipment>().AsNoTracking().Where(s => s.FactoryId == factoryId && !s.IsReturn);
        q = status switch
        {
            "transit" => q.Where(s => s.Status >= ShipmentStatus.AwaitingPickup && s.Status <= ShipmentStatus.OutForDelivery),
            "delivered" => q.Where(s => s.Status == ShipmentStatus.Delivered),
            "refused" => q.Where(s => s.Status == ShipmentStatus.Refused),
            _ => q,
        };
        return await Shipments(q.OrderByDescending(s => s.Id).Take(200)).ToListAsync(ct);
    }

    public Task<FactoryShipmentDto?> ShipmentAsync(long factoryId, long shipmentId, CancellationToken ct) =>
        Shipments(db.Set<Shipment>().Where(s => s.FactoryId == factoryId && s.Id == shipmentId)).FirstOrDefaultAsync(ct);

    private IQueryable<FactoryShipmentDto> Shipments(IQueryable<Shipment> q) => q.AsNoTracking().Select(s => new FactoryShipmentDto(
        s.Id, s.Order.Code, s.Provider, s.TrackingNumber, s.Status.ToString(), s.CodAmount,
        db.Set<OrderItem>().Where(i => i.OrderId == s.OrderId && i.Kind == OrderItemKind.PrintedDesign && i.Status != OrderItemStatus.Cancelled).Sum(i => i.Qty),
        s.CreatedAt, City(s.Order.AddressJson)));

    public Task<ShipmentLabel?> LabelAsync(long factoryId, long shipmentId, CancellationToken ct) =>
        db.Set<Shipment>().AsNoTracking().Where(s => s.FactoryId == factoryId && s.Id == shipmentId).Select(s => new ShipmentLabel(
            s.TrackingNumber, s.Provider, s.Order.Code, s.Order.Customer.FullName, s.Order.Phone, s.Order.AddressJson, s.CodAmount,
            db.Set<OrderItem>().Where(i => i.OrderId == s.OrderId && i.Kind == OrderItemKind.PrintedDesign && i.Status != OrderItemStatus.Cancelled).Sum(i => i.Qty),
            s.Factory!.Name, s.Factory.Address, s.CreatedAt)).FirstOrDefaultAsync(ct);

    /// <summary>Earned per piece for every job that left the factory (price list, else the catalogue cost; + back print), less deductions.</summary>
    public async Task<FactoryMoneyDto> MoneyAsync(long factoryId, CancellationToken ct)
    {
        var rows = await db.Set<Job>().AsNoTracking().Where(j => j.FactoryId == factoryId && (j.Stage == JobStage.Shipped || j.Stage == JobStage.Ready || j.DeductionAmount > 0))
            .OrderByDescending(j => j.Id).Select(j => new
            {
                j.Code, Order = j.OrderItem.Order.Code, j.CreatedAt, j.OrderItem.Title, j.OrderItem.Qty, j.DeductionAmount, j.IsSample, j.Stage,
                Price = db.Set<FactoryPrice>().Where(p => p.FactoryId == factoryId && p.CatalogProductId == j.OrderItem.Design!.CatalogProductId)
                    .Select(p => (decimal?)(p.PricePerPiece + (j.OrderItem.Design!.BackArtFileId != null ? p.BackPrintPrice : 0))).FirstOrDefault(),
                Cost = j.OrderItem.Design != null ? j.OrderItem.Design.CatalogProduct.FactoryCost : 0,
            }).ToListAsync(ct);
        var lines = rows.Select(r => new FactoryMoneyLineDto(r.Code, r.Order, r.CreatedAt, r.Title, r.Qty,
            r.Stage is JobStage.Shipped or JobStage.Ready ? (r.Price ?? r.Cost) * r.Qty : 0, r.DeductionAmount, r.IsSample)).ToList();
        var earned = lines.Sum(l => l.Earning);
        var deductions = lines.Sum(l => l.Deduction);
        return new FactoryMoneyDto(earned, deductions, earned - deductions, lines.Where(l => l.Earning > 0).Sum(l => l.Qty), lines);
    }

    public async Task<FactoryReportDto> ReportAsync(long factoryId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = from.ToDateTime(TimeOnly.MinValue);
        var end = to.ToDateTime(TimeOnly.MaxValue);
        var events = await db.Set<JobEvent>().AsNoTracking().Where(e => e.Job.FactoryId == factoryId && e.At >= start && e.At <= end)
            .Select(e => new { e.JobId, e.FromStage, e.ToStage, e.At, e.Job.DueAt, StaffName = e.Staff != null ? e.Staff.Name : null, Role = e.Staff != null ? (FactoryRole?)e.Staff.Role : null })
            .ToListAsync(ct);
        var done = events.Where(e => e.ToStage == (byte)JobStage.Ready).ToList();
        var onTime = done.Count(e => e.At <= e.DueAt);
        var fails = events.Count(e => e.FromStage == (byte)JobStage.QualityCheck && e.ToStage == (byte)JobStage.Printing);
        var qcChecks = events.Count(e => e.FromStage == (byte)JobStage.QualityCheck);
        var staff = events.Where(e => e.StaffName is not null).GroupBy(e => (e.StaffName!, e.Role!.Value)).Select(g => new StaffReportDto(g.Key.Item1, g.Key.Item2.ToString(), g.Count(),
            g.Count(e => e.ToStage == (byte)JobStage.QualityCheck), g.Count(e => e.FromStage == (byte)JobStage.QualityCheck && e.ToStage == (byte)JobStage.Packing),
            g.Count(e => e.FromStage == (byte)JobStage.QualityCheck && e.ToStage == (byte)JobStage.Printing), g.Count(e => e.ToStage == (byte)JobStage.Ready)))
            .OrderByDescending(s => s.Steps).ToList();
        return new FactoryReportDto(from, to, done.Count, onTime, done.Count == 0 ? 0 : Math.Round(100m * onTime / done.Count, 1), fails,
            qcChecks == 0 ? 0 : Math.Round(100m * fails / qcChecks, 1), staff);
    }
}
