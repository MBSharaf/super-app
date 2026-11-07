using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.Games;
using SuperApp.Application.Features.Pitches;
using SuperApp.Application.Features.Shop.Orders;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Cancellations;

/// <summary>
/// The things a customer can ask to cancel. <see cref="DescribeAsync"/> checks the item is the customer's, still
/// cancellable and paid online; <see cref="CancelAsync"/> runs the normal cancel (refund, slots, stock, notices) once
/// the request is approved.
/// </summary>
public sealed class CancellationTargets(
    IMyBookingsService bookings,
    IMyOrdersService orders,
    IGamesService games,
    ICancellationRepository repository,
    IClock clock)
{
    public async Task<CancellationSubject> DescribeAsync(CancellationKind kind, string reference, Guid customerId, CancellationToken ct)
    {
        var subject = kind switch
        {
            CancellationKind.Booking => await BookingAsync(reference, customerId, ct),
            CancellationKind.Package => await PackageAsync(reference, customerId, ct),
            CancellationKind.Order => await OrderAsync(reference, customerId, ct),
            _ => await GameAsync(reference, customerId, ct),
        };
        return subject.Paid > 0 ? subject
            : throw new BusinessRuleException("Nothing was paid online for this, so you can cancel it straight away.", "no_payment");
    }

    /// <summary>Cancels the item for an approved request and returns what was refunded.</summary>
    public async Task<decimal> CancelAsync(CancellationKind kind, string reference, Guid customerId, CancellationToken ct)
    {
        switch (kind)
        {
            case CancellationKind.Booking:
                return await bookings.CancelApprovedAsync(reference, customerId, "Cancellation request approved", ct);
            case CancellationKind.Order:
                return await orders.CancelApprovedAsync(reference, customerId, ct);
            case CancellationKind.Game:
                return await games.WithdrawApprovedAsync(reference, customerId, ct);
            default:
                var refund = 0m;
                foreach (var code in (await PackageGamesAsync(reference, customerId, ct)).BookingCodes)
                {
                    refund += await bookings.CancelApprovedAsync(code, customerId, $"Package {reference}: cancellation request approved", ct);
                }
                return refund;
        }
    }

    private async Task<CancellationSubject> BookingAsync(string code, Guid customerId, CancellationToken ct)
    {
        var (b, pitch, preview) = await bookings.ForRequestAsync(code, customerId, ct);
        if (!preview.CanCancel) { throw new BusinessRuleException("This booking can no longer be cancelled.", "cannot_cancel"); }
        return new CancellationSubject($"{pitch} · {b.Date:yyyy-MM-dd} {b.StartHour:00}:00", $"#/booking/{b.Code}", Deposits.PaidOnline(b), preview.RefundAmount,
            preview.RefundPercent == 100 ? "Full refund" : preview.RefundPercent == 50 ? "Half refund" : "No refund", null);
    }

    private async Task<CancellationSubject> PackageAsync(string code, Guid customerId, CancellationToken ct)
    {
        var (pitch, codes) = await PackageGamesAsync(code, customerId, ct);
        decimal paid = 0, refund = 0;
        foreach (var c in codes)
        {
            var (b, _, preview) = await bookings.ForRequestAsync(c, customerId, ct);
            (paid, refund) = (paid + Deposits.PaidOnline(b), refund + (preview.CanCancel ? preview.RefundAmount : 0));
        }
        return new CancellationSubject($"{pitch} · {codes.Count} remaining weekly games", "#/bookings", paid, refund, "Each game by the policy", null);
    }

    private async Task<(string PitchName, IReadOnlyList<string> BookingCodes)> PackageGamesAsync(string code, Guid customerId, CancellationToken ct)
    {
        var found = await repository.PackageAsync(code, customerId, DateOnly.FromDateTime(CairoTime.Now(clock)), ct) ?? throw new NotFoundException("Package not found.");
        return found.BookingCodes.Count > 0 ? found : throw new BusinessRuleException("This package has no games left to cancel.", "nothing_left");
    }

    private async Task<CancellationSubject> OrderAsync(string code, Guid customerId, CancellationToken ct)
    {
        var (order, paid) = await orders.ForRequestAsync(code, customerId, ct);
        var count = order.Items.Sum(i => i.Qty);
        return new CancellationSubject($"Order {order.Code} · {count} item{(count > 1 ? "s" : "")}", $"#/order/{order.Code}", paid, paid, null, null);
    }

    private async Task<CancellationSubject> GameAsync(string code, Guid customerId, CancellationToken ct)
    {
        var (game, mine) = await games.ForRequestAsync(code, customerId, ct);
        var paid = mine.Payment is { Status: PaymentStatus.Paid } p ? p.Amount : 0;
        return new CancellationSubject($"Game {game.Code} · {game.Booking.Date:yyyy-MM-dd} {game.Booking.StartHour:00}:00", $"#/players/{game.Code}", paid, paid, null, null);
    }
}
