using SuperApp.Application.Common.Responses;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Application.Features.Bookings;

/// <summary>Bookings, their hours and holds in the database.</summary>
public interface IBookingRepository
{
    Task<BookablePitch?> PitchAsync(string slug, CancellationToken ct);

    /// <summary>
    /// Holds the hours for this person for 10 minutes. Expired holds are cleared first; an hour held by someone
    /// else or already booked throws ConflictException (409).
    /// </summary>
    Task<DateTime> HoldAsync(long pitchId, IReadOnlyList<(DateOnly Date, int Hour)> slots, Guid userId, CancellationToken ct);

    /// <summary>Saves the booking with one slot per hour. Another booking won the hour → ConflictException (409).</summary>
    Task SaveAsync(Booking booking, IReadOnlyList<(DateOnly Date, int Hour)> slots, CancellationToken ct);

    /// <summary>The same person's booking of exactly these hours made in the last minutes (a repeated request).</summary>
    Task<Booking?> RecentSameAsync(Guid userId, long pitchId, DateOnly date, int startHour, int duration, CancellationToken ct);

    Task<Booking?> FindAsync(string code, CancellationToken ct);

    Task<PagedResult<Booking>> MineAsync(Guid userId, string tab, DateOnly today, int page, int pageSize, CancellationToken ct);

    /// <summary>Frees the booking's hours (after a cancellation).</summary>
    Task ReleaseSlotsAsync(long bookingId, CancellationToken ct);

    Task<string> NewCodeAsync(CancellationToken ct);

    Task<(string Slug, string Name, string Address)> PitchInfoAsync(long pitchId, CancellationToken ct);

    /// <summary>The code of the package a match belongs to.</summary>
    Task<string?> PackageCodeAsync(long packageId, CancellationToken ct);

    Task<Booking?> ByPaymentAsync(long paymentId, CancellationToken ct);

    Task<IReadOnlyList<Booking>> AllByPaymentAsync(long paymentId, CancellationToken ct);
}

/// <summary>Platform settings (ops.Settings) as numbers.</summary>
public interface ISettingsReader
{
    Task<decimal> DecimalAsync(string key, decimal fallback, CancellationToken ct);
}

/// <summary>Signed QR tokens for bookings and packages, and the QR picture.</summary>
public interface IQrTokens
{
    string Create(string code);

    /// <summary>The code inside a token, or null when the token was changed or is not ours.</summary>
    string? Read(string token);

    byte[] Png(string token);
}
