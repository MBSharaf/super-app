using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.AdminConsole;

/// <summary>Read side of the admin console: lists, drawers, overview, audit.</summary>
public interface IAdminReader
{
    Task<PagedResult<CustomerRowDto>> CustomersAsync(PeopleQuery query, CancellationToken ct);

    Task<CustomerDetailDto?> CustomerAsync(Guid id, CancellationToken ct);

    Task<PagedResult<PartnerRowDto>> PartnersAsync(PeopleQuery query, DateTime now, CancellationToken ct);

    Task<PartnerDetailDto?> PartnerAsync(long id, DateTime now, CancellationToken ct);

    Task<PagedResult<SellerRowDto>> SellersAsync(PeopleQuery query, CancellationToken ct);

    Task<SellerDetailDto?> SellerAsync(long id, CancellationToken ct);

    Task<PagedResult<OrderRowDto>> OrdersAsync(OrdersQuery query, CancellationToken ct);

    Task<PagedResult<BookingRowDto>> BookingsAsync(OrdersQuery query, CancellationToken ct);

    Task<PagedResult<AuditRowDto>> AuditAsync(AuditQuery query, CancellationToken ct);

    Task<AdminOverviewDto> OverviewAsync(DateTime now, CancellationToken ct);
}

/// <summary>Write side for people: the tracked rows the admin actions change.</summary>
public interface IAdminPeopleRepository
{
    /// <summary>A customer account (a person who is not staff), or null.</summary>
    Task<User?> CustomerAsync(Guid id, CancellationToken ct);

    Task<Partner?> PartnerAsync(long id, CancellationToken ct);

    Task<Seller?> SellerAsync(long id, CancellationToken ct);

    /// <summary>A pitch with its venue and the venue's partner, or null.</summary>
    Task<Pitch?> PitchAsync(long id, CancellationToken ct);
}

/// <summary>ops.Settings rows.</summary>
public interface IPlatformSettingsRepository
{
    Task<IReadOnlyList<Setting>> ListAsync(string? group, CancellationToken ct);
}

/// <summary>Broadcasts and their recipients.</summary>
public interface IBroadcastRepository
{
    Task<PagedResult<BroadcastDto>> ListAsync(int page, int pageSize, CancellationToken ct);

    void Add(Broadcast broadcast);

    Task<Broadcast?> GetAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<Broadcast>> DueAsync(DateTime now, CancellationToken ct);

    /// <summary>Puts the message in the bell of every active account in the segment; returns how many.</summary>
    Task<int> DeliverAsync(Broadcast broadcast, CancellationToken ct);
}

/// <summary>Data for the reports (Excel with a dashboard sheet).</summary>
public interface IReportsReader
{
    Task<ReportData> BuildAsync(string kind, DateTime from, DateTime to, CancellationToken ct);

    Task<ReportData> AuditAsync(AuditQuery query, CancellationToken ct);
}

/// <summary>Kinds of reports (CEO reports and admin exports). "revenue" is another name for finance; marketing is not on the server yet.</summary>
public static class ReportKinds
{
    public static readonly IReadOnlySet<string> Available = new HashSet<string> { "operations", "customers", "partners", "sellers", "support", "agents", "finance", "revenue", "approvals" };
    public static readonly IReadOnlySet<string> MoneyLater = new HashSet<string> { "marketing" };

    public static BroadcastSegment? Segment(string value) =>
        Enum.TryParse<BroadcastSegment>(value, true, out var s) && !int.TryParse(value, out _) ? s : null;
}
