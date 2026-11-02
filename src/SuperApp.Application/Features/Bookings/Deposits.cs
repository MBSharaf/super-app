using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Application.Features.Bookings;

/// <summary>What a booking's customer pays in the app (the deposit) and at the pitch (the rest).</summary>
public readonly record struct DepositSplit(decimal Deposit, decimal BalanceDue);

/// <summary>
/// Owner's rule (2026-11-01): a pitch booking pays only a deposit online — <c>deposit.percent</c> of the total (20%) with a
/// minimum of <c>deposit.min</c> (100 EGP), never more than the total. The rest is paid in cash to the venue at the pitch and
/// recorded when the venue scans the QR. Weekly packages: the rule applies to each match.
/// </summary>
public static class Deposits
{
    public const decimal DefaultPercent = 20m;
    public const decimal DefaultMinimum = 100m;

    public static DepositSplit Split(decimal total, decimal percent, decimal minimum)
    {
        if (total <= 0)
        {
            return new DepositSplit(0, 0);
        }
        var deposit = Math.Min(total, Math.Max(Math.Round(total * percent / 100, 0, MidpointRounding.AwayFromZero), minimum));
        return new DepositSplit(deposit, total - deposit);
    }

    public static async Task<DepositSplit> SplitAsync(ISettingsReader settings, decimal total, CancellationToken ct) =>
        Split(total, await settings.DecimalAsync("deposit.percent", DefaultPercent, ct), await settings.DecimalAsync("deposit.min", DefaultMinimum, ct));

    /// <summary>Sets the deposit and the rest on a booking from its total.</summary>
    public static async Task ApplyAsync(ISettingsReader settings, Booking booking, CancellationToken ct)
    {
        var split = await SplitAsync(settings, booking.Total, ct);
        booking.Deposit = split.Deposit;
        booking.BalanceDue = split.BalanceDue;
    }

    /// <summary>
    /// What the customer paid in the app: the deposit; a booking from before the rule was paid in full; cash bookings nothing.
    /// Refunds are a share of this.
    /// </summary>
    public static decimal PaidOnline(Booking b) => b.PayAtVenue ? 0 : b.Total - b.BalanceDue;
}
