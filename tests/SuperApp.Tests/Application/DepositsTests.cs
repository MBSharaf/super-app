using Shouldly;
using SuperApp.Application.Features.Bookings;
using SuperApp.Application.Features.CheckIn;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Tests.Application;

/// <summary>Owner's rule (2026-11-01): a pitch booking pays 20% online (at least 100 EGP); the booked hour never moves.</summary>
public sealed class DepositsTests
{
    [Theory]
    [InlineData(670, 134, 536)]   // 20% is more than the minimum
    [InlineData(400, 100, 300)]   // 20% = 80 → the 100 EGP minimum
    [InlineData(80, 80, 0)]       // never more than the total
    [InlineData(503, 101, 402)]   // 100.6 rounds to 101
    [InlineData(0, 0, 0)]
    public void The_deposit_is_20_percent_with_a_100_egp_minimum(decimal total, decimal deposit, decimal rest)
    {
        Deposits.Split(total, Deposits.DefaultPercent, Deposits.DefaultMinimum).ShouldBe(new DepositSplit(deposit, rest));
    }

    [Fact]
    public void Paid_online_is_the_deposit_or_the_whole_total_for_older_bookings()
    {
        Deposits.PaidOnline(new Booking { Total = 670, Deposit = 134, BalanceDue = 536 }).ShouldBe(134);
        Deposits.PaidOnline(new Booking { Total = 670 }).ShouldBe(670);
        Deposits.PaidOnline(new Booking { Total = 670, PayAtVenue = true }).ShouldBe(0);
    }

    [Theory]
    [InlineData(16, 30, 0, 120)]   // 30 minutes early: the whole two hours are left
    [InlineData(17, 15, 15, 105)]  // 15 minutes late: 105 minutes left
    [InlineData(18, 15, 75, 45)]   // 75 minutes late: 45 minutes left of the 2nd hour
    [InlineData(19, 5, 120, 0)]    // after the end
    public void A_late_player_sees_what_is_left_of_the_booked_time(int hour, int minute, int late, int left)
    {
        var booking = new Booking { Date = new DateOnly(2026, 11, 5), StartHour = 17, Duration = 2 };
        var now = new DateTime(2026, 11, 5, hour, minute, 0);
        Timing.Of(booking, now).ShouldBe(new Timing(late, left, 19));
    }
}
