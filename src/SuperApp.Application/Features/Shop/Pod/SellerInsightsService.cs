using SuperApp.Application.Abstractions.Documents;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Shop.Pod;

/// <summary>The seller dashboard (API §4.4): overview with the level card, orders, analytics by level (Excel from Elite) and the marketing calendar.</summary>
public interface ISellerInsightsService
{
    Task<SellerOverviewDto> OverviewAsync(CancellationToken ct);

    Task<PagedResult<SellerOrderDto>> OrdersAsync(SellerOrdersQuery query, CancellationToken ct);

    Task<SellerAnalyticsDto> AnalyticsAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    Task<(byte[] File, string Name)> AnalyticsExcelAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    Task<IReadOnlyList<CalendarEventDto>> CalendarAsync(CancellationToken ct);
}

public sealed class SellerInsightsService(
    ISellerAccess access,
    IDesignRepository designs,
    ISellerInsightsRepository insights,
    ISellerLevels levels,
    ISettingsReader settings,
    IExcelExporter excel,
    IClock clock) : ISellerInsightsService
{
    public async Task<SellerOverviewDto> OverviewAsync(CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        var kpis = await insights.KpisAsync(me.SellerId, me.StoreId, (int)await settings.DecimalAsync("pod.holdDays", 7, ct), ct);
        var next = await levels.NextAsync(me.Level.Rank, ct);
        var card = new LevelCardDto(me.Level.Key, me.Level.NameEn, me.Level.NameAr, me.Level.Rank, next?.Key, next?.NameEn, next?.SalesNeeded ?? me.Level.SalesNeeded,
            next?.MinRating ?? me.Level.MinRating, me.LifetimeSales, me.Rating, me.Level.ActiveDesigns, await designs.ActiveDesignsAsync(me.SellerId, ct), me.FrozenUntil);
        var all = await designs.DesignsAsync(me.SellerId, null, me.Level.BaseDiscount, ct);
        var latest = await insights.OrdersAsync(me.StoreId, new SellerOrdersQuery(PageSize: 5), ct);
        return new SellerOverviewDto(card, kpis, all.Where(d => d.Status == nameof(DesignStatus.Live)).OrderByDescending(d => d.Sales).ThenByDescending(d => d.Views).Take(5).ToList(), latest.Items);
    }

    public async Task<PagedResult<SellerOrderDto>> OrdersAsync(SellerOrdersQuery query, CancellationToken ct)
    {
        if (query.Status is not (null or "open" or "delivered" or "cancelled" or "all") || query.Page < 1 || query.PageSize is < 1 or > 100)
        {
            throw new BadRequestException("Status is open, delivered, cancelled or all; page size 1–100.", "bad_query");
        }
        var me = await access.CurrentAsync(ct);
        return await insights.OrdersAsync(me.StoreId, query, ct);
    }

    public async Task<SellerAnalyticsDto> AnalyticsAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        var (start, end) = Range(from, to);
        var (days, perDesign) = await insights.AnalyticsAsync(me.SellerId, me.StoreId, start.ToDateTime(TimeOnly.MinValue), end.ToDateTime(TimeOnly.MaxValue), ct);
        var advanced = me.Level.AnalyticsTier != AnalyticsTier.Basic;
        return new SellerAnalyticsDto(start, end, me.Level.AnalyticsTier.ToString(), perDesign.Sum(d => d.Items), perDesign.Sum(d => d.Earnings), perDesign.Sum(d => d.Views),
            advanced ? days : [], perDesign);
    }

    public async Task<(byte[] File, string Name)> AnalyticsExcelAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        if (me.Level.AnalyticsTier != AnalyticsTier.AdvancedWithExport)
        {
            throw new ForbiddenException("Excel export opens at the Elite level.", "export_level");
        }
        var a = await AnalyticsAsync(from, to, ct);
        var file = excel.Build(
            new ExcelDashboard($"{me.StoreName} — analytics", $"{a.From:yyyy-MM-dd} to {a.To:yyyy-MM-dd}",
                [new ExcelFigure("Items sold", a.Items), new ExcelFigure("Earnings", a.Earnings, true), new ExcelFigure("Design views (all time)", a.Views)]),
            new ExcelSheet("Days", ["Date", "Items", "Earnings (EGP)"], a.Days.Select(d => (IReadOnlyList<object?>)[d.Date.ToDateTime(TimeOnly.MinValue), d.Items, d.Earnings]).ToList()),
            new ExcelSheet("Designs", ["Design", "Views", "Items", "Earnings (EGP)"], a.Designs.Select(d => (IReadOnlyList<object?>)[d.Title, d.Views, d.Items, d.Earnings]).ToList()));
        return (file, $"analytics-{me.Handle}-{a.From:yyyyMMdd}-{a.To:yyyyMMdd}.xlsx");
    }

    /// <summary>Public holidays and shopping days ahead; how far ahead depends on the level (1, 3, 6 or 12 months).</summary>
    public async Task<IReadOnlyList<CalendarEventDto>> CalendarAsync(CancellationToken ct)
    {
        var me = await access.CurrentAsync(ct);
        var today = DateOnly.FromDateTime(clock.UtcNow);
        var end = today.AddMonths(me.Level.CalendarTier switch { 0 => 1, 1 => 3, 2 => 6, _ => 12 });
        var events = (await insights.HolidaysAsync(today, end, ct)).ToList();
        for (var year = today.Year; year <= end.Year; year++)
        {
            events.Add(new CalendarEventDto(LastFriday(year, 11), "White Friday sales", "الجمعة البيضاء", "Shopping"));
            events.Add(new CalendarEventDto(new DateOnly(year, 9, 15), "Back to school", "العودة للمدارس", "Shopping"));
        }
        return events.Where(e => e.Date >= today && e.Date <= end).OrderBy(e => e.Date).ToList();
    }

    private (DateOnly From, DateOnly To) Range(DateOnly? from, DateOnly? to)
    {
        var end = to ?? DateOnly.FromDateTime(clock.UtcNow);
        var start = from ?? end.AddDays(-29);
        if (start > end || end.DayNumber - start.DayNumber > 366)
        {
            throw new BadRequestException("Choose a range of up to one year, oldest date first.", "bad_range");
        }
        return (start, end);
    }

    private static DateOnly LastFriday(int year, int month)
    {
        var d = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        while (d.DayOfWeek != DayOfWeek.Friday) { d = d.AddDays(-1); }
        return d;
    }
}

/// <summary>The seller levels in order (lookup pod.SellerLevels).</summary>
public interface ISellerLevels
{
    Task<Domain.Entities.Pod.SellerLevel?> NextAsync(byte rank, CancellationToken ct);
}

