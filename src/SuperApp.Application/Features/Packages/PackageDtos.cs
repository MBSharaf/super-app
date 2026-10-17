using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Application.Features.Packages;

/* ---------- Partner: package sizes ---------- */

/// <summary>A package size of a pitch. Status: WaitingForApproval, Approved, Rejected, Disabled.</summary>
public sealed record PartnerPackageOptionDto(long Id, int MatchCount, decimal DiscountPercent, string Status, decimal? ApprovedDiscount, string? RejectReason);

public sealed record PackageOptionInput(int MatchCount, decimal DiscountPercent);

/// <summary>PUT /partner/pitches/{id}/package-options — the whole list; new or changed sizes wait for approval, missing ones are switched off.</summary>
public sealed record PackageOptionsRequest(IReadOnlyList<PackageOptionInput> Options);

/* ---------- Admin: approvals ---------- */

public sealed record PackageOptionReviewDto(long Id, long PitchId, string PitchName, string BusinessName, int MatchCount, decimal DiscountPercent, decimal PricePerHour, string Status, DateTime CreatedAt);

/// <summary>Approve as asked, or with a lower discount.</summary>
public sealed record ApproveOptionRequest(decimal? ApprovedDiscount);

public sealed record RejectOptionRequest(string Reason);

/* ---------- Customers: quote, buy, my packages ---------- */

/// <summary>GET /packages/quote — <c>StartsOn</c> is the first match day (its weekday repeats); <c>Hour</c> a grid hour (25 = 1 AM).</summary>
public sealed record PackageQuoteQuery(string PitchSlug, long OptionId, DateOnly StartsOn, int Hour, int Duration = 1);

public sealed record QuoteMatch(DateOnly Date, int Hour, bool Available, decimal ListPrice, decimal Price);

/// <summary>Weeks already taken are skipped; the package runs up to 4 extra weeks to reach its size.</summary>
public sealed record PackageQuoteDto(long OptionId, int MatchCount, decimal DiscountPercent, IReadOnlyList<QuoteMatch> Weeks, int Skipped,
    decimal ListPrice, decimal DiscountAmount, decimal ServiceFee, decimal Total);

/// <summary>POST /packages — paid straight away until payments arrive (Phase 5). <c>AcceptWaiver</c> must be true.</summary>
public sealed record BuyPackageRequest(string PitchSlug, long OptionId, DateOnly StartsOn, int Hour, int Duration, bool AcceptWaiver);

/// <summary>One match of a package. Status as for bookings (Confirmed, CheckedIn, Played, NoShow, Cancelled…).</summary>
public sealed record PackageMatchDto(int Number, string Code, DateOnly Date, int StartHour, string Status, DateTime? CheckedInAt, decimal Total, decimal DiscountAmount);

/// <summary>A package as its customer sees it. <c>QrToken</c> (one QR for every match) only on the package's own page.</summary>
public sealed record PackageDto(
    string Code,
    string PitchSlug,
    string PitchName,
    int MatchCount,
    int UsedCount,
    int DayOfWeek,
    int StartHour,
    int Duration,
    DateOnly StartsOn,
    DateOnly EndsOn,
    decimal DiscountPercent,
    decimal TotalPrice,
    string Status,
    IReadOnlyList<PackageMatchDto> Matches,
    string? QrToken);

/* ---------- Database ---------- */

public interface IPackageRepository
{
    Task<IReadOnlyList<PartnerPackageOptionDto>> OptionsAsync(long pitchId, CancellationToken ct);

    /// <summary>Every option row of the pitch (any status), tracked for changes.</summary>
    Task<List<PackageOption>> OptionRowsAsync(long pitchId, CancellationToken ct);

    void AddOption(PackageOption option);

    Task<PagedResult<PackageOptionReviewDto>> WaitingOptionsAsync(PageRequest page, CancellationToken ct);

    /// <summary>An option with the pitch owner's user id (for the notifications), tracked for changes.</summary>
    Task<(PackageOption Option, string PitchName, Guid OwnerUserId)?> OptionForReviewAsync(long id, CancellationToken ct);

    /// <summary>An approved option of a live pitch.</summary>
    Task<PackageOption?> ApprovedOptionAsync(string pitchSlug, long optionId, CancellationToken ct);

    /// <summary>Saves the package and its bookings (one slot per hour) in one transaction; a taken hour → ConflictException.</summary>
    Task SaveAsync(Package package, IReadOnlyList<(Booking Booking, IReadOnlyList<(DateOnly Date, int Hour)> Slots)> matches, CancellationToken ct);

    Task<string> NewCodeAsync(CancellationToken ct);

    Task<Package?> RecentSameAsync(Guid customerId, long pitchId, DateOnly startsOn, int startHour, CancellationToken ct);

    Task<IReadOnlyList<PackageDto>> MineAsync(Guid customerId, CancellationToken ct);

    Task<PackageDto?> GetAsync(Guid customerId, string code, CancellationToken ct);
}
