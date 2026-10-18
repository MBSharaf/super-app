using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Factory;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Enums;
using FactoryEntity = SuperApp.Domain.Entities.Factories.Factory;

namespace SuperApp.Infrastructure.Shop;

/// <summary>The production board, job pages and the overview.</summary>
public sealed partial class FactoryRepository
{
    internal sealed record JobRow(long Id, string Code, JobStage Stage, DateTime DueAt, string OrderCode, string Title, string ProductName, string Size, string Color, int Qty,
        bool HasFront, bool HasBack, string? AssignedTo, byte ReprintCount, bool IsSample, string AddressJson);

    public async Task<FactoryOverviewDto> OverviewAsync(long factoryId, DateTime now, CancellationToken ct)
    {
        var jobs = db.Set<Job>().AsNoTracking().Where(j => j.FactoryId == factoryId);
        var stages = await jobs.GroupBy(j => j.Stage).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var dayStart = now.Date;
        var late = await jobs.CountAsync(j => j.Stage < JobStage.Ready && j.DueAt < now, ct);
        var dueToday = await jobs.CountAsync(j => j.Stage < JobStage.Ready && j.DueAt < dayStart.AddDays(1), ct);
        var doneToday = await db.Set<JobEvent>().CountAsync(e => e.Job.FactoryId == factoryId && e.ToStage == (byte)JobStage.Ready && e.At >= dayStart, ct);
        var problems = await db.Set<JobProblem>().CountAsync(p => p.Job.FactoryId == factoryId && p.Status == JobProblemStatus.Open, ct);
        var transit = await db.Set<Shipment>().CountAsync(s => s.FactoryId == factoryId && !s.IsReturn && s.Status >= ShipmentStatus.AwaitingPickup && s.Status <= ShipmentStatus.OutForDelivery, ct);
        var capacity = await db.Set<FactoryEntity>().Where(f => f.Id == factoryId).Select(f => f.CapacityPerDay).SingleAsync(ct);
        int N(JobStage s) => stages.GetValueOrDefault(s);
        return new FactoryOverviewDto(N(JobStage.New), N(JobStage.Printing), N(JobStage.QualityCheck), N(JobStage.Packing), N(JobStage.Ready), late, dueToday, capacity, doneToday, problems, transit);
    }

    public async Task<PagedResult<FactoryJobCardDto>> JobsAsync(long factoryId, FactoryJobsQuery query, long? staffId, DateTime now, CancellationToken ct) =>
        await JobPageAsync(db.Set<Job>().Where(j => j.FactoryId == factoryId), query.Stage, query.Mine ? staffId ?? -1 : null, query.Late, query.Q, query.Page, query.PageSize, now, ct);

    internal static async Task<PagedResult<FactoryJobCardDto>> JobPageAsync(IQueryable<Job> q, string? stage, long? assignedTo, bool late, string? text, int page, int pageSize, DateTime now, CancellationToken ct)
    {
        q = stage switch
        {
            null or "" or "active" => q.Where(j => j.Stage < JobStage.Ready),
            "all" => q,
            _ => Enum.TryParse<JobStage>(stage, true, out var s) ? q.Where(j => j.Stage == s) : q.Where(_ => false),
        };
        if (assignedTo is not null) { q = q.Where(j => j.AssignedStaffId == assignedTo); }
        if (late) { q = q.Where(j => j.Stage < JobStage.Ready && j.DueAt < now); }
        if (!string.IsNullOrWhiteSpace(text)) { q = q.Where(j => j.Code.Contains(text) || j.OrderItem.Order.Code.Contains(text) || j.OrderItem.Title.Contains(text)); }
        var total = await q.CountAsync(ct);
        var rows = await Rows(q.OrderBy(j => j.DueAt).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct);
        return new PagedResult<FactoryJobCardDto>(rows.Select(r => Card(r, now)).ToList(), page, pageSize, total);
    }

    internal static IQueryable<JobRow> Rows(IQueryable<Job> q) => q.AsNoTracking().Select(j => new JobRow(
        j.Id, j.Code, j.Stage, j.DueAt, j.OrderItem.Order.Code, j.OrderItem.Title, j.OrderItem.Design != null ? j.OrderItem.Design.CatalogProduct.NameEn : "",
        j.OrderItem.Size, j.OrderItem.Color, j.OrderItem.Qty, j.OrderItem.Design != null && j.OrderItem.Design.FrontArtFileId != null,
        j.OrderItem.Design != null && j.OrderItem.Design.BackArtFileId != null, j.AssignedStaff != null ? j.AssignedStaff.Name : null, j.ReprintCount, j.IsSample, j.OrderItem.Order.AddressJson));

    internal static FactoryJobCardDto Card(JobRow r, DateTime now) => new(
        r.Code, r.Stage.ToString(), r.DueAt, r.Stage < JobStage.Ready && r.DueAt < now, r.OrderCode, r.Title, r.ProductName, r.Size, r.Color, r.Qty, r.HasFront, r.HasBack,
        r.AssignedTo, r.ReprintCount, r.IsSample, City(r.AddressJson));

    public async Task<FactoryJobDto?> JobDtoAsync(long factoryId, string code, DateTime now, CancellationToken ct)
    {
        var row = await Rows(db.Set<Job>().Where(j => j.FactoryId == factoryId && j.Code == code)).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }
        var designId = await db.Set<Job>().Where(j => j.Id == row.Id).Select(j => j.OrderItem.DesignId).SingleAsync(ct);
        var design = await db.Set<Design>().AsNoTracking().IgnoreQueryFilters().Include(d => d.CatalogProduct).ThenInclude(c => c.PrintAreas).FirstOrDefaultAsync(d => d.Id == designId, ct);
        var events = await db.Set<JobEvent>().AsNoTracking().Where(e => e.JobId == row.Id).OrderBy(e => e.At).ThenBy(e => e.Id)
            .Select(e => new JobEventDto(((JobStage)e.FromStage).ToString(), ((JobStage)e.ToStage).ToString(), e.Staff != null ? e.Staff.Name : null, e.Note, e.At)).ToListAsync(ct);
        var problems = await db.Set<JobProblem>().AsNoTracking().Where(p => p.JobId == row.Id).OrderByDescending(p => p.Id)
            .Select(p => new JobProblemDto(p.Id, p.Kind.ToString(), p.Detail, p.Status.ToString(), p.Resolution, p.CreatedAt)).ToListAsync(ct);
        var deduction = await db.Set<Job>().Where(j => j.Id == row.Id).Select(j => j.DeductionAmount).SingleAsync(ct);
        return new FactoryJobDto(Card(row, now), design?.CatalogProduct.Key ?? "", row.HasFront ? $"/api/v1/factory/jobs/{row.Code}/art/front" : null,
            row.HasBack ? $"/api/v1/factory/jobs/{row.Code}/art/back" : null, Placement(design?.FrontX, design?.FrontY, design?.FrontW), Placement(design?.BackX, design?.BackY, design?.BackW),
            design?.CatalogProduct.PrintAreas.OrderBy(a => a.Side).Select(a => new PrintAreaDto(a.Side.ToString(), a.X, a.Y, a.Width, a.Height, a.PrintWidthCm)).ToList() ?? [],
            events, problems, deduction);
    }

    public Task<Job?> JobAsync(long factoryId, string code, CancellationToken ct) =>
        db.Set<Job>().Include(j => j.OrderItem).ThenInclude(i => i.Order).ThenInclude(o => o.Items).FirstOrDefaultAsync(j => j.FactoryId == factoryId && j.Code == code, ct);

    public async Task<IReadOnlyList<Job>> OrderJobsAsync(long orderId, CancellationToken ct) =>
        await db.Set<Job>().Where(j => j.OrderItem.OrderId == orderId).ToListAsync(ct);

    public Task<long?> ArtFileAsync(long factoryId, string code, int side, CancellationToken ct) =>
        db.Set<Job>().Where(j => j.FactoryId == factoryId && j.Code == code && j.OrderItem.Design != null)
            .Select(j => side == 1 ? j.OrderItem.Design!.BackArtFileId : j.OrderItem.Design!.FrontArtFileId).FirstOrDefaultAsync(ct);

    private static PlacementDto? Placement(decimal? x, decimal? y, decimal? w) => x is null ? null : new PlacementDto(x.Value, y ?? 0, w ?? 0);

    internal static string City(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("city", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
        }
        catch (JsonException)
        {
            return "";
        }
    }
}
