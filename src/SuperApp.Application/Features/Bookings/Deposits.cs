using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Bookings;

/// <summary>What a booking's customer pays in the app (the deposit) and at the pitch (the rest).</summary>
public readonly record struct DepositSplit(decimal Deposit, decimal BalanceDue);

/// <summary>
/// Each pitch's owner chooses how players pay (owner's rule, 2026-11-21, <see cref="PitchPaymentMode"/>):
/// <list type="bullet">
/// <item><c>Deposit</c> (rule of 2026-11-01): a deposit online — <c>deposit.percent</c> of the total (20%) with a minimum of
/// <c>deposit.min</c> (100 EGP), never more than the total; the rest in cash at the pitch, recorded when the QR is scanned.</item>
/// <item><c>FullOnline</c>: the whole total online, nothing at the pitch; Super Malaab pays the venue its share after the match.</item>
/// </list>
/// Weekly packages: the rule applies to each match.
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

    /// <summary>The split for a pitch's payment mode: paid in full online, or the deposit rule.</summary>
    public static DepositSplit Split(decimal total, decimal percent, decimal minimum, PitchPaymentMode mode) =>
        mode == PitchPaymentMode.FullOnline ? new DepositSplit(Math.Max(0, total), 0) : Split(total, percent, minimum);

    public static async Task<DepositSplit> SplitAsync(ISettingsReader settings, decimal total, PitchPaymentMode mode, CancellationToken ct) =>
        Split(total, await settings.DecimalAsync("deposit.percent", DefaultPercent, ct), await settings.DecimalAsync("deposit.min", DefaultMinimum, ct), mode);

    /// <summary>Sets the deposit and the rest on a booking from its total and its pitch's payment mode.</summary>
    public static async Task ApplyAsync(ISettingsReader settings, Booking booking, PitchPaymentMode mode, CancellationToken ct)
    {
        var split = await SplitAsync(settings, booking.Total, mode, ct);
        booking.PaymentMode = mode;
        booking.Deposit = split.Deposit;
        booking.BalanceDue = split.BalanceDue;
    }

    /// <summary>
    /// What the customer paid in the app: the deposit; a booking from before the rule was paid in full; cash bookings nothing.
    /// Refunds are a share of this.
    /// </summary>
    public static decimal PaidOnline(Booking b) => b.PayAtVenue ? 0 : b.Total - b.BalanceDue;
}
