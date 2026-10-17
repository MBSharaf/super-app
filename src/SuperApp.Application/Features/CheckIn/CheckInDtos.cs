using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Application.Features.CheckIn;

/// <summary>
/// POST /check-in/scan — the scanned QR <c>Token</c>, or the booking <c>Code</c> typed by the staff when the camera
/// can't read it. <c>PitchId</c> (optional) is the pitch the staff is checking players in at; without it any pitch of
/// the business is accepted.
/// </summary>
public sealed record ScanRequest(string? Token, string? Code, long? PitchId, string? Device);

/// <summary>
/// The answer to a scan. <c>Result</c>: accepted or rejected. <c>Reason</c> (when rejected): bad_signature, other_venue,
/// wrong_day, wrong_hour, cancelled, already_used, package_finished, account_blocked. <c>Match</c>: "3 of 8" for packages.
/// </summary>
public sealed record ScanResultDto(
    string Result,
    string? Reason,
    string Message,
    string? Code,
    string? CustomerName,
    string? PitchName,
    DateOnly? Date,
    int? StartHour,
    int? Duration,
    string? Match);

/// <summary>GET /check-in/history?pitchId&amp;date — one row per scan.</summary>
public sealed record ScanRowDto(long Id, DateTime ScannedAt, string Result, string? Code, string? CustomerName, string ScannedBy, int? MatchNumber, string? Device);

public sealed record CheckInHistoryQuery(long PitchId, DateOnly? Date = null);

/// <summary>What a scan needs about a booking (tracked for changes).</summary>
public sealed record ScanTarget(Booking Booking, long PartnerId, string PitchName, string CustomerName, bool CustomerBlocked);

/// <summary>What a package scan needs: the package and its matches in date order (tracked for changes).</summary>
public sealed record PackageTarget(Package Package, long PartnerId, string PitchName, string CustomerName, bool CustomerBlocked, IReadOnlyList<Booking> Matches);

public interface ICheckInRepository
{
    /// <summary>The scan already recorded with this key (a repeated request), with its match's booking code and the package size.</summary>
    Task<(QrScan Scan, string? Code, int? MatchCount)?> ByKeyAsync(string idempotencyKey, CancellationToken ct);

    Task<ScanTarget?> BookingAsync(string code, CancellationToken ct);

    Task<PackageTarget?> PackageAsync(string code, CancellationToken ct);

    void Add(QrScan scan);

    Task<IReadOnlyList<ScanRowDto>> HistoryAsync(long pitchId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
}
