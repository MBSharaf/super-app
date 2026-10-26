using FluentValidation;
using SuperApp.Application.Abstractions.Notifications;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Partners;
using SuperApp.Application.Features.Pitches;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.CheckIn;

/// <summary>
/// QR check-in at the venue (API §3.6). A rejected scan is a normal answer (200, result "rejected" with the reason);
/// the same Idempotency-Key always gets the same answer, so a double tap never checks in twice.
/// Players can check in from 60 minutes before kick-off until the end of their booking.
/// </summary>
public interface ICheckInService
{
    Task<ScanResultDto> ScanAsync(ScanRequest request, string idempotencyKey, CancellationToken ct);

    Task<IReadOnlyList<ScanRowDto>> HistoryAsync(CheckInHistoryQuery query, CancellationToken ct);
}

public sealed class CheckInService(
    IPartnerAccess access,
    IPartnerBookingsRepository venues,
    ICheckInRepository scans,
    IQrTokens qr,
    IUnitOfWork unitOfWork,
    INotifier notifier,
    ICurrentUser currentUser,
    IClock clock,
    IValidator<ScanRequest> rules,
    IAuditLogger audit,
    Money.LoyaltyService loyalty) : ICheckInService
{
    public static readonly TimeSpan EarlyWindow = TimeSpan.FromMinutes(60);

    public async Task<ScanResultDto> ScanAsync(ScanRequest request, string idempotencyKey, CancellationToken ct)
    {
        await rules.ValidateAndThrowAsync(request, ct);
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 64)
        {
            throw new BadRequestException("Send an Idempotency-Key header (up to 64 characters) with each scan.", "idempotency_key");
        }
        var scope = await access.DeskAsync(ct);
        if (request.PitchId is { } pitchId && await venues.PitchAsync(scope.PartnerId, pitchId, ct) is null)
        {
            throw new ForbiddenException("You can only check players in at your own pitches.", "other_venue_staff");
        }
        if (await scans.ByKeyAsync(idempotencyKey, ct) is { } repeated)
        {
            var again = repeated.Code is null ? null : await scans.BookingAsync(repeated.Code, ct);
            return Answer(repeated.Scan.Result, again, MatchText(repeated.Scan.MatchNumber, repeated.MatchCount));
        }
        var code = string.IsNullOrWhiteSpace(request.Token) ? request.Code!.Trim().ToUpperInvariant() : qr.Read(request.Token.Trim());
        if (code?.StartsWith("PK-", StringComparison.Ordinal) == true)
        {
            return await ScanPackageAsync(code, scope, request, idempotencyKey, ct);
        }
        var target = code is null ? null : await scans.BookingAsync(code, ct);
        if (target is null)
        {
            return Answer(QrScanResult.BadSignature, null);
        }
        var result = Judge(target, scope, request.PitchId);
        await RecordAsync(target, null, result, request, idempotencyKey, null, ct);
        return Answer(result, target);
    }

    public async Task<IReadOnlyList<ScanRowDto>> HistoryAsync(CheckInHistoryQuery query, CancellationToken ct)
    {
        var scope = await access.ViewAsync(ct);
        _ = await venues.PitchAsync(scope.PartnerId, query.PitchId, ct) ?? throw new NotFoundException("Pitch not found.");
        var day = query.Date ?? DateOnly.FromDateTime(CairoTime.Now(clock));
        var from = CairoTime.ToUtc(day.ToDateTime(TimeOnly.MinValue));
        return await scans.HistoryAsync(query.PitchId, from, from.AddDays(1), ct);
    }

    /// <summary>One QR for a whole package: finds today's match, checks it in and counts it.</summary>
    private async Task<ScanResultDto> ScanPackageAsync(string code, PartnerScope scope, ScanRequest request, string key, CancellationToken ct)
    {
        var t = await scans.PackageAsync(code, ct);
        if (t is null)
        {
            return Answer(QrScanResult.BadSignature, null);
        }
        var (result, match) = JudgePackage(t, scope, request.PitchId);
        var number = match is null ? (int?)null : t.Matches.Where(Live).ToList().IndexOf(match) + 1;
        if (result == QrScanResult.Accepted)
        {
            t.Package.UsedCount++;
            t.Package.Status = t.Package.UsedCount >= t.Package.MatchCount ? PackageStatus.Finished : t.Package.Status;
        }
        var target = match is null ? null : new ScanTarget(match, t.PartnerId, t.PitchName, t.CustomerName, t.CustomerBlocked);
        await RecordAsync(target, t.Package.Id, result, request with { PitchId = request.PitchId ?? t.Package.PitchId }, key, (byte?)number, ct);
        var answer = Answer(result, target, MatchText(number, t.Package.MatchCount));
        return answer with { Code = answer.Code ?? t.Package.Code, CustomerName = t.CustomerName, PitchName = t.PitchName };
    }

    private (QrScanResult Result, Booking? Match) JudgePackage(PackageTarget t, PartnerScope scope, long? pitchId)
    {
        var p = t.Package;
        if (t.PartnerId != scope.PartnerId || (pitchId is not null && p.PitchId != pitchId)) { return (QrScanResult.OtherVenue, null); }
        if (p.Status is PackageStatus.Cancelled) { return (QrScanResult.Cancelled, null); }
        if (t.CustomerBlocked) { return (QrScanResult.AccountBlocked, null); }
        if (p.Status is PackageStatus.Finished or PackageStatus.Expired || p.UsedCount >= p.MatchCount) { return (QrScanResult.PackageFinished, null); }
        var now = CairoTime.Now(clock);
        var live = t.Matches.Where(Live).ToList();
        var current = live.FirstOrDefault(b => InWindow(b, now));
        if (current is not null)
        {
            return (current.Status is BookingStatus.CheckedIn or BookingStatus.Played ? QrScanResult.AlreadyUsed : QrScanResult.Accepted, current);
        }
        var today = live.FirstOrDefault(b => b.Date == DateOnly.FromDateTime(now));
        return today is not null ? (QrScanResult.WrongHour, today) : (QrScanResult.WrongDay, live.FirstOrDefault(b => Start(b) > now));
    }

    private static bool Live(Booking b) => b.Status is not (BookingStatus.CancelledByCustomer or BookingStatus.CancelledByVenue);

    private static DateTime Start(Booking b) => b.Date.ToDateTime(new TimeOnly(b.StartHour, 0));

    private static bool InWindow(Booking b, DateTime now) => now >= Start(b) - EarlyWindow && now < Start(b).AddHours(b.Duration);

    private static string? MatchText(int? number, int? count) => number is null || count is null ? null : $"{number} of {count}";

    /// <summary>The rules, in the order the staff should hear them.</summary>
    private QrScanResult Judge(ScanTarget t, PartnerScope scope, long? pitchId)
    {
        var b = t.Booking;
        if (t.PartnerId != scope.PartnerId || (pitchId is not null && b.PitchId != pitchId)) { return QrScanResult.OtherVenue; }
        if (b.Status is BookingStatus.CancelledByCustomer or BookingStatus.CancelledByVenue) { return QrScanResult.Cancelled; }
        if (t.CustomerBlocked) { return QrScanResult.AccountBlocked; }
        if (b.Status is BookingStatus.CheckedIn or BookingStatus.Played || b.CheckedInAt is not null) { return QrScanResult.AlreadyUsed; }
        var now = CairoTime.Now(clock);
        var start = b.Date.ToDateTime(new TimeOnly(b.StartHour, 0));
        var end = start.AddHours(b.Duration);
        if (now >= start - EarlyWindow && now < end) { return QrScanResult.Accepted; }
        var day = (now - EarlyWindow).Date == start.Date || now.Date == start.Date;
        return day ? QrScanResult.WrongHour : QrScanResult.WrongDay;
    }

    private async Task RecordAsync(ScanTarget? t, long? packageId, QrScanResult result, ScanRequest request, string key, byte? matchNumber, CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        if (result == QrScanResult.Accepted && t is not null)
        {
            t.Booking.Status = BookingStatus.CheckedIn;
            t.Booking.CheckedInAt = clock.UtcNow;
            await loyalty.EarnForBookingAsync(t.Booking, ct);
        }
        scans.Add(new QrScan
        {
            BookingId = t?.Booking.Id, PackageId = packageId, PitchId = request.PitchId ?? t!.Booking.PitchId, ScannedById = me, ScannedAt = clock.UtcNow,
            Result = result, MatchNumber = matchNumber, Device = request.Device?.Trim() is { Length: > 0 } d ? d[..Math.Min(d.Length, 100)] : null, IdempotencyKey = key,
        });
        await unitOfWork.SaveChangesAsync(ct);
        if (result == QrScanResult.Accepted && t is not null)
        {
            await audit.LogAsync(AuditSystem.Partner, "checkin.accepted", $"Checked in {t.Booking.Code} at {t.PitchName}", "Booking", t.Booking.Code,
                new { pitch = t.PitchName, customer = t.CustomerName, match = matchNumber }, ct);
        }
        if (result == QrScanResult.Accepted && t?.Booking.CustomerId is { } customer)
        {
            await notifier.NotifyUserAsync(customer, new NotificationMessage(NotificationCategory.Bookings, $"Checked in at {t.PitchName}",
                $"{t.Booking.Code} · have a great game!", $"#/booking/{t.Booking.Code}", NotificationTone.Success), ct);
        }
    }

    private static ScanResultDto Answer(QrScanResult result, ScanTarget? t, string? match = null)
    {
        var b = t?.Booking;
        var (reason, message) = Reasons[result];
        return new ScanResultDto(result == QrScanResult.Accepted ? "accepted" : "rejected", reason, match is null ? message : $"{message} Match {match}.",
            b?.Code, t?.CustomerName, t?.PitchName, b?.Date, b?.StartHour, b?.Duration, match);
    }

    private static readonly Dictionary<QrScanResult, (string? Reason, string Message)> Reasons = new()
    {
        [QrScanResult.Accepted] = (null, "Checked in. Have a great game!"),
        [QrScanResult.BadSignature] = ("bad_signature", "This code isn’t a Super App booking. Ask for the QR in the app or the booking ID."),
        [QrScanResult.OtherVenue] = ("other_venue", "This booking is for another pitch."),
        [QrScanResult.WrongDay] = ("wrong_day", "This booking is for another day."),
        [QrScanResult.WrongHour] = ("wrong_hour", "Too early or too late: check-in opens 60 minutes before kick-off and closes when the booking ends."),
        [QrScanResult.Cancelled] = ("cancelled", "This booking was cancelled."),
        [QrScanResult.AlreadyUsed] = ("already_used", "Already checked in."),
        [QrScanResult.PackageFinished] = ("package_finished", "Every match of this package was used."),
        [QrScanResult.AccountBlocked] = ("account_blocked", "This player’s account is blocked. Contact Super App support."),
    };
}

public sealed class ScanRequestValidator : AbstractValidator<ScanRequest>
{
    public ScanRequestValidator()
    {
        RuleFor(x => x.PitchId).GreaterThan(0).When(x => x.PitchId is not null);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Token) || !string.IsNullOrWhiteSpace(x.Code))
            .WithName("token").WithMessage("Scan the QR or type the booking ID.");
        RuleFor(x => x.Token).MaximumLength(1000);
        RuleFor(x => x.Code).MaximumLength(40);
        RuleFor(x => x.Device).MaximumLength(100);
    }
}
