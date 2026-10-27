using System.Text.Json;
using SuperApp.Application.Abstractions.Documents;

namespace SuperApp.Application.Features.AdminConsole;

public sealed record PeopleQuery(string? Q = null, string? Status = null, string? Level = null, int Page = 1, int PageSize = 20);

/// <summary>Spent = bookings (not cancelled) and shop orders (not cancelled or refunded) through Super App.</summary>
public sealed record CustomerRowDto(Guid Id, string FullName, string? Email, string? Phone, string Status, string MustPrepay, int CodRefusals,
    DateTime CreatedAt, DateTime? LastSeenAt, int Bookings, int Orders, decimal Spent);

/// <summary>A wallet credit Super App gave the customer.</summary>
public sealed record CreditRowDto(decimal Amount, string? Reason, DateTime At);

/// <summary>The customer drawer: profile, admin note, latest bookings and orders, open tickets, wallet, points and credits.</summary>
public sealed record CustomerDetailDto(CustomerRowDto Customer, string? AdminNote, string? BlockedReason, string Language,
    IReadOnlyList<BookingRowDto> RecentBookings, IReadOnlyList<OrderRowDto> RecentOrders, int OpenTickets,
    decimal WalletBalance, int Points, IReadOnlyList<CreditRowDto> Credits);

/// <summary>
/// One pitch partner with its last 30 days: gross (what customers paid), commission (Super App's share), booked hours against
/// the opening hours of its live pitches (occupancy), closed pitches and its bookings wallet (available money).
/// </summary>
public sealed record PartnerRowDto(long Id, Guid UserId, string BusinessName, string Owner, string? Email, string? Phone, string Status,
    int Venues, int Pitches, int Bookings30d, DateTime CreatedAt, int ClosedPitches, decimal Gross30d, decimal Commission30d,
    int BookedHours30d, int OpenHoursWeek, decimal Balance, decimal? BookingCommission, decimal? StoreCommission)
{
    public decimal Occupancy30d => OpenHoursWeek == 0 ? 0 : Math.Min(1, Math.Round(BookedHours30d / (OpenHoursWeek * 30m / 7), 3));
}

public sealed record VenueBriefDto(long Id, string Name, int Pitches);

/// <summary>A pitch in the partner drawer (close / reopen from there).</summary>
public sealed record PitchBriefDto(long Id, string Slug, string Name, string Venue, decimal PricePerHour, byte Format, string Status);

public sealed record PartnerDetailDto(PartnerRowDto Partner, string? SuspendedReason, bool SlotPostingBlocked, IReadOnlyList<VenueBriefDto> Venues,
    IReadOnlyList<PitchBriefDto> PitchList);

public sealed record SellerRowDto(long Id, Guid UserId, string Name, string? Email, string Store, string Level, string Mode, string Status,
    int LifetimeSales, decimal Rating, bool Featured, DateTime? FrozenUntil, int LiveDesigns, DateTime CreatedAt);

public sealed record SellerDetailDto(SellerRowDto Seller, int Strikes, int Followers);

public sealed record OrderRowDto(string Code, string Customer, string Status, string PaymentMethod, int Items, decimal Total, DateTime CreatedAt);

public sealed record BookingRowDto(string Code, string Pitch, string Venue, string? Customer, DateOnly Date, int StartHour, int Duration,
    string Status, string Source, decimal Total, DateTime CreatedAt);

/// <summary>?status &amp;q (code or customer) &amp;from &amp;to (Cairo dates).</summary>
public sealed record OrdersQuery(string? Status = null, string? Q = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 20);

public sealed record AuditRowDto(long Id, DateTime At, string System, string Action, string? Actor, string? Agent, string? EntityType, string? EntityId,
    string Summary, string? Ip);

/// <summary>?actor (name or email) &amp;action (starts with) &amp;entity &amp;system &amp;from &amp;to.</summary>
public sealed record AuditQuery(string? Actor = null, string? Action = null, string? Entity = null, string? System = null, DateOnly? From = null,
    DateOnly? To = null, int Page = 1, int PageSize = 50);

/// <summary>GET /admin/overview — counts only (money comes with Phase 5).</summary>
public sealed record AdminOverviewDto(int Customers, int NewCustomers7d, int Partners, int Sellers, int BookingsToday, int OrdersToday,
    int OpenTickets, int UrgentTickets, int PendingApplications, int OpenCommunityReports, int ActiveAgents, IReadOnlyList<AuditRowDto> Latest);

public sealed record ReasonInput(string Reason);

public sealed record PrepayRequest(string Rule);

public sealed record NoteRequest(string Note);

public sealed record FeatureRequest(bool Featured);

/// <summary>POST /admin/customers/{id}/credit — goodwill money to their Super App wallet, paid by Super App (5–5,000 EGP).</summary>
public sealed record CreditRequest(decimal Amount, string Reason);

/// <summary>POST /admin/customers/{id}/points — + adds, − removes (never below 0); they see the reason.</summary>
public sealed record PointsRequest(int Points, string Reason);

/// <summary>PUT /admin/partners/{id}/commission — percent (0–50); null = the platform rate.</summary>
public sealed record CommissionDealRequest(decimal? Bookings, decimal? Store, string? Note);

/// <summary>POST /admin/partners/{id}/adjust — + or − on the bookings wallet's available money, with Super App on the other side.</summary>
public sealed record BalanceAdjustRequest(decimal Amount, string Reason);

/// <summary>A platform setting; money settings are shown but changed only from the CEO limits (Phase 5).</summary>
public sealed record SettingDto(string Key, string Group, JsonElement Value, bool Editable, DateTime UpdatedAt);

/// <summary>PUT /admin/settings/{group} — { values: { "popups.timing": {...} } }.</summary>
public sealed record UpdateSettingsRequest(Dictionary<string, JsonElement> Values);

public sealed record MaintenanceDto(bool BookingsPaused, bool ShopPaused, string? Message);

public sealed record MaintenanceRequest(bool PauseBookings, bool PauseShop, string? Message);

public sealed record BroadcastDto(long Id, string Title, string Body, string? Link, string Segment, bool SendEmail, DateTime? ScheduledAt,
    DateTime? SentAt, int Recipients, DateTime CreatedAt);

/// <summary>POST /admin/broadcasts — now (201) or at <c>scheduledAt</c> (202). Segment Everyone / Customers / Partners / Sellers / Factories / FantasyPlayers.</summary>
public sealed record BroadcastRequest(string Title, string Body, string? Link, string Segment, bool SendEmail, DateTime? ScheduledAt);

/// <summary>One report: its dashboard figures and data sheet.</summary>
public sealed record ReportData(string Title, IReadOnlyList<ExcelFigure> Figures, IReadOnlyList<ExcelSheet> Sheets);
