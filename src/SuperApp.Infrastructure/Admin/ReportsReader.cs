using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Documents;
using SuperApp.Application.Features.AdminConsole;
using SuperApp.Application.Features.Money;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Admin;

/// <summary>Builds the non-money reports: figures for the dashboard sheet and one data sheet (rows capped at 50,000).</summary>
public sealed class ReportsReader(AppDbContext db, AdminReader reader, IRevenueReader revenue) : IReportsReader
{
    private const int MaxRows = 50_000;

    public Task<ReportData> BuildAsync(string kind, DateTime from, DateTime to, CancellationToken ct) => kind switch
    {
        "operations" => OperationsAsync(from, to, ct),
        "customers" => CustomersAsync(from, to, ct),
        "partners" => PartnersAsync(to, ct),
        "sellers" => SellersAsync(ct),
        "support" => SupportAsync(from, to, ct),
        "finance" or "revenue" => FinanceAsync(from, to, ct),
        "approvals" => ApprovalsAsync(from, to, ct),
        _ => AgentsAsync(from, to, ct),
    };

    public async Task<ReportData> AuditAsync(AuditQuery query, CancellationToken ct)
    {
        var rows = await AdminReader.AuditRows(reader.FilterAudit(query)).Take(MaxRows).ToListAsync(ct);
        return new ReportData("Audit log", [new("Lines", rows.Count), new("By agents", rows.Count(r => r.Agent != null))],
            [Sheet("Audit", ["At (UTC)", "System", "Action", "Person", "Agent", "Entity", "Id", "Summary", "IP"],
                rows.Select(r => Row(r.At, r.System, r.Action, r.Actor, r.Agent, r.EntityType, r.EntityId, r.Summary, r.Ip)))]);
    }

    private async Task<ReportData> OperationsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var (f, t) = (DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
        var bookings = await db.Set<Booking>().Where(b => b.Date >= f && b.Date < t).GroupBy(b => b.Date)
            .Select(g => new { Day = g.Key, Count = g.Count(), Cancelled = g.Count(b => b.Status == BookingStatus.CancelledByCustomer || b.Status == BookingStatus.CancelledByVenue), NoShow = g.Count(b => b.Status == BookingStatus.NoShow) })
            .ToListAsync(ct);
        var orders = await db.Set<Order>().Where(o => o.CreatedAt >= from && o.CreatedAt < to).Select(o => new { o.CreatedAt, o.Status }).ToListAsync(ct);
        var byDay = orders.GroupBy(o => DateOnly.FromDateTime(o.CreatedAt)).ToDictionary(g => g.Key, g => g.Count());
        var days = Enumerable.Range(0, t.DayNumber - f.DayNumber).Select(f.AddDays).ToList();
        return new ReportData("Operations", [new("Bookings", bookings.Sum(b => b.Count)), new("Cancelled bookings", bookings.Sum(b => b.Cancelled)),
                new("No-shows", bookings.Sum(b => b.NoShow)), new("Orders", orders.Count), new("Cancelled orders", orders.Count(o => o.Status == OrderStatus.Cancelled))],
            [Sheet("By day", ["Day", "Bookings", "Cancelled", "No-shows", "Orders"], days.Select(d =>
            {
                var b = bookings.FirstOrDefault(x => x.Day == d);
                return Row(d.ToDateTime(TimeOnly.MinValue), b?.Count ?? 0, b?.Cancelled ?? 0, b?.NoShow ?? 0, byDay.GetValueOrDefault(d));
            }))]);
    }

    private async Task<ReportData> CustomersAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await reader.CustomerRows(reader.Customers().Where(u => u.CreatedAt >= from && u.CreatedAt < to).OrderBy(u => u.CreatedAt)).Take(MaxRows).ToListAsync(ct);
        return new ReportData("New customers", [new("New customers", rows.Count), new("Blocked", rows.Count(r => r.Status != "Active")), new("Booked at least once", rows.Count(r => r.Bookings > 0))],
            [Sheet("Customers", ["Name", "Email", "Phone", "Status", "Joined", "Bookings", "Orders"],
                rows.Select(r => Row(r.FullName, r.Email, r.Phone, r.Status, r.CreatedAt, r.Bookings, r.Orders)))]);
    }

    private async Task<ReportData> PartnersAsync(DateTime to, CancellationToken ct)
    {
        var rows = (await reader.PartnersAsync(new PeopleQuery(PageSize: 100), to, ct)).Total;
        var all = await db.Set<Partner>().AsNoTracking().OrderBy(p => p.BusinessName).Take(MaxRows)
            .Select(p => new { p.BusinessName, Owner = p.User.FullName, p.User.Email, p.Status, Pitches = db.Set<Pitch>().Count(x => x.Venue.PartnerId == p.Id), p.CreatedAt }).ToListAsync(ct);
        return new ReportData("Partners", [new("Partners", rows), new("Suspended", all.Count(p => p.Status == PartnerStatus.Suspended)), new("Pitches", all.Sum(p => p.Pitches))],
            [Sheet("Partners", ["Business", "Owner", "Email", "Status", "Pitches", "Joined"], all.Select(p => Row(p.BusinessName, p.Owner, p.Email, p.Status.ToString(), p.Pitches, p.CreatedAt)))]);
    }

    private async Task<ReportData> SellersAsync(CancellationToken ct)
    {
        var rows = await reader.SellerRows(db.Set<Seller>().OrderByDescending(s => s.LifetimeSales)).Take(MaxRows).ToListAsync(ct);
        return new ReportData("Sellers", [new("Sellers", rows.Count), new("Live designs", rows.Sum(r => r.LiveDesigns)), new("Items sold (lifetime)", rows.Sum(r => r.LifetimeSales))],
            [Sheet("Sellers", ["Name", "Store", "Level", "Mode", "Status", "Sold", "Rating", "Live designs", "Featured"],
                rows.Select(r => Row(r.Name, r.Store, r.Level, r.Mode, r.Status, r.LifetimeSales, r.Rating, r.LiveDesigns, r.Featured ? "Yes" : "No")))]);
    }

    private async Task<ReportData> SupportAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await db.Set<Ticket>().AsNoTracking().Where(t => t.CreatedAt >= from && t.CreatedAt < to).OrderBy(t => t.CreatedAt).Take(MaxRows)
            .Select(t => new { t.Code, t.Subject, t.Status, t.Priority, t.CreatedAt, t.FirstResponseAt, t.ResolvedAt }).ToListAsync(ct);
        var answered = rows.Where(r => r.FirstResponseAt != null).Select(r => (r.FirstResponseAt!.Value - r.CreatedAt).TotalHours).ToList();
        return new ReportData("Support", [new("Tickets", rows.Count), new("Resolved", rows.Count(r => r.ResolvedAt != null)),
                new("Average first reply (hours)", answered.Count == 0 ? 0 : Math.Round((decimal)answered.Average(), 1))],
            [Sheet("Tickets", ["Code", "Subject", "Status", "Priority", "Opened", "First reply", "Resolved"],
                rows.Select(r => Row(r.Code, r.Subject, r.Status.ToString(), r.Priority.ToString(), r.CreatedAt, r.FirstResponseAt, r.ResolvedAt)))]);
    }

    private async Task<ReportData> AgentsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await db.Set<AgentRun>().AsNoTracking().Where(r => r.StartedAt >= from && r.StartedAt < to).OrderBy(r => r.StartedAt).Take(MaxRows)
            .Select(r => new { Agent = r.Agent.Name, r.StartedAt, r.FinishedAt, r.Result, r.Summary, r.InputTokens, r.OutputTokens, r.CostUsd }).ToListAsync(ct);
        return new ReportData("AI agents", [new("Runs", rows.Count), new("Failed", rows.Count(r => r.Result == AgentRunResult.Failed)), new("Cost (USD)", rows.Sum(r => r.CostUsd))],
            [Sheet("Runs", ["Agent", "Started", "Finished", "Result", "Summary", "Input tokens", "Output tokens", "Cost (USD)"],
                rows.Select(r => Row(r.Agent, r.StartedAt, r.FinishedAt, r.Result.ToString(), r.Summary, r.InputTokens, r.OutputTokens, r.CostUsd)))]);
    }

    /// <summary>Platform revenue by Cairo day (the revenue wallet): income streams, costs, net and money through Super App.</summary>
    private async Task<ReportData> FinanceAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var (f, t) = (DateOnly.FromDateTime(from), DateOnly.FromDateTime(to));
        var (days, _, _) = await revenue.ReadAsync(CairoTime.ToUtc(from), CairoTime.ToUtc(to), ct);
        var byDay = days.ToDictionary(d => d.Date);
        var all = Enumerable.Range(0, t.DayNumber - f.DayNumber).Select(f.AddDays).Select(d => byDay.GetValueOrDefault(d) ?? new RevenueDayDto(d, 0, 0, 0, 0, 0, 0)).ToList();
        static decimal Income(RevenueDayDto d) => d.Bookings + d.Fees + d.Passes;
        static decimal Costs(RevenueDayDto d) => d.Refunds + d.Promos;
        return new ReportData("Finance", [new("Income", all.Sum(Income), true), new("Costs", all.Sum(Costs), true), new("Net", all.Sum(d => Income(d) - Costs(d)), true),
                new("Money through Super App", all.Sum(d => d.Gmv), true)],
            [Sheet("By day", ["Day", "Commission (EGP)", "Service fees (EGP)", "Fantasy Pass (EGP)", "Refunds (EGP)", "Vouchers (EGP)", "Net (EGP)", "Through Super App (EGP)"],
                all.Select(d => Row(d.Date.ToDateTime(TimeOnly.MinValue), d.Bookings, d.Fees, d.Passes, d.Refunds, d.Promos, Income(d) - Costs(d), d.Gmv)))]);
    }

    private async Task<ReportData> ApprovalsAsync(DateTime from, DateTime to, CancellationToken ct)
    {
        var rows = await db.Set<Approval>().AsNoTracking().Where(a => a.CreatedAt >= from && a.CreatedAt < to).OrderBy(a => a.CreatedAt).Take(MaxRows)
            .Select(a => new { a.CreatedAt, a.Kind, a.Summary, a.Amount, a.Status, By = a.RequestedBy.FullName, a.DecidedAt }).ToListAsync(ct);
        return new ReportData("Approvals", [new("Requests", rows.Count), new("Approved", rows.Count(r => r.Status == ApprovalStatus.Approved)),
                new("Rejected", rows.Count(r => r.Status == ApprovalStatus.Rejected)), new("Amount approved", rows.Where(r => r.Status == ApprovalStatus.Approved).Sum(r => r.Amount), true)],
            [Sheet("Approvals", ["Asked", "Kind", "What", "Amount (EGP)", "Status", "Asked by", "Decided"],
                rows.Select(r => Row(r.CreatedAt, r.Kind.ToString(), r.Summary, r.Amount, r.Status.ToString(), r.By, r.DecidedAt)))]);
    }

    private static ExcelSheet Sheet(string name, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows) => new(name, headers, rows.ToList());

    private static object?[] Row(params object?[] cells) => cells;
}
