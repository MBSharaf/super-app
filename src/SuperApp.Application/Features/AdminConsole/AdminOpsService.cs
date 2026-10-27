using FluentValidation;
using SuperApp.Application.Abstractions.Documents;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.CeoSystem;

namespace SuperApp.Application.Features.AdminConsole;

/// <summary>
/// Overview, orders and bookings (read), the audit log, and the non-money reports — every export is an Excel file with
/// a dashboard sheet first (rule 8). Money reports (finance, revenue, marketing spend) answer 422 until Phase 5.
/// </summary>
public interface IAdminOpsService
{
    Task<AdminOverviewDto> OverviewAsync(CancellationToken ct);

    Task<PagedResult<OrderRowDto>> OrdersAsync(OrdersQuery query, CancellationToken ct);

    Task<PagedResult<BookingRowDto>> BookingsAsync(OrdersQuery query, CancellationToken ct);

    Task<PagedResult<AuditRowDto>> AuditAsync(AuditQuery query, CancellationToken ct);

    Task<ExcelFile> AuditExportAsync(AuditQuery query, CancellationToken ct);

    Task<ExcelFile> ReportAsync(string kind, ReportQuery query, CancellationToken ct);

    /// <summary>The same report as data (figures and sheets) for the CEO's reports screen.</summary>
    Task<ReportData> ReportDataAsync(string kind, ReportQuery query, CancellationToken ct);
}

public sealed class AdminOpsService(IAdminReader reader, IReportsReader reports, IExcelExporter excel, IAuditLogger audit, IClock clock) : IAdminOpsService
{
    public Task<AdminOverviewDto> OverviewAsync(CancellationToken ct) => reader.OverviewAsync(clock.UtcNow, ct);

    public Task<PagedResult<OrderRowDto>> OrdersAsync(OrdersQuery query, CancellationToken ct) => reader.OrdersAsync(query, ct);

    public Task<PagedResult<BookingRowDto>> BookingsAsync(OrdersQuery query, CancellationToken ct) => reader.BookingsAsync(query, ct);

    public Task<PagedResult<AuditRowDto>> AuditAsync(AuditQuery query, CancellationToken ct) => reader.AuditAsync(query, ct);

    public async Task<ExcelFile> AuditExportAsync(AuditQuery query, CancellationToken ct)
    {
        var data = await reports.AuditAsync(query, ct);
        await audit.LogAsync(Domain.Enums.AuditSystem.Admin, "audit.exported", "Audit log exported", ct: ct);
        return Build(data, "audit", $"{query.From?.ToString("yyyy-MM-dd") ?? "start"} – {query.To?.ToString("yyyy-MM-dd") ?? "today"}");
    }

    /// <summary>?from&amp;to (default: the last 30 days, at most a year).</summary>
    public async Task<ExcelFile> ReportAsync(string kind, ReportQuery query, CancellationToken ct)
    {
        var (data, from, to) = await BuildAsync(kind, query, ct);
        await audit.LogAsync(Domain.Enums.AuditSystem.Admin, "report.exported", $"Report {kind} {from:yyyy-MM-dd} – {to:yyyy-MM-dd}", ct: ct);
        return Build(data, kind.ToLowerInvariant(), $"{from:yyyy-MM-dd} – {to:yyyy-MM-dd}");
    }

    public async Task<ReportData> ReportDataAsync(string kind, ReportQuery query, CancellationToken ct) => (await BuildAsync(kind, query, ct)).Data;

    private async Task<(ReportData Data, DateOnly From, DateOnly To)> BuildAsync(string kind, ReportQuery query, CancellationToken ct)
    {
        kind = kind.ToLowerInvariant();
        if (ReportKinds.MoneyLater.Contains(kind))
        {
            throw new BusinessRuleException("This report is not on the server yet.", "report_later");
        }
        if (!ReportKinds.Available.Contains(kind))
        {
            throw new NotFoundException("This report does not exist.");
        }
        var to = query.To ?? DateOnly.FromDateTime(clock.UtcNow);
        var from = query.From ?? to.AddDays(-29);
        if (from > to || to.DayNumber - from.DayNumber > 366)
        {
            throw new BadRequestException("Choose a range of up to one year.", "from");
        }
        var data = await reports.BuildAsync(kind, from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), ct);
        return (data, from, to);
    }

    private ExcelFile Build(ReportData data, string name, string range) =>
        new($"superapp-{name}-{clock.UtcNow:yyyyMMdd}.xlsx", excel.Build(new ExcelDashboard(data.Title, range, data.Figures), [.. data.Sheets]));
}

public sealed class OrdersQueryValidator : AbstractValidator<OrdersQuery>
{
    public OrdersQueryValidator()
    {
        RuleFor(x => x.Q).MaximumLength(100);
        RuleFor(x => x.Status).MaximumLength(40);
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class AuditQueryValidator : AbstractValidator<AuditQuery>
{
    public AuditQueryValidator()
    {
        RuleFor(x => x.Actor).MaximumLength(100);
        RuleFor(x => x.Action).MaximumLength(80);
        RuleFor(x => x.Entity).MaximumLength(60);
        RuleFor(x => x.System).MaximumLength(20);
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
