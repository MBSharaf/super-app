using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Tests.Api;

/// <summary>
/// Owner's question (2026-11-17): many players book the same pitch and hour at the same moment. 100 players, 5 per pitch on
/// 20 pitches, all at once: each pitch gets exactly one booking, only the winners pay, the others are told the hour is taken.
/// </summary>
[Trait("Category", "Database")]
public sealed class BookingRushTests(AccountsApiFactory api) : IClassFixture<AccountsApiFactory>
{
    private const int Pitches = 20;
    private const int PlayersPerPitch = 5;
    private readonly PartnerKit kit = new(api);

    [Fact]
    public async Task One_hundred_players_at_once_get_one_booking_per_pitch_and_only_the_winners_pay()
    {
        var pitches = new List<string>();
        for (var i = 0; i < Pitches; i++)
        {
            pitches.Add((await kit.PartnerWithPitchAsync(live: true)).Pitch.GetProperty("slug").GetString()!);
        }
        var players = new List<(ApiClient Client, Guid Id, string Pitch)>();
        foreach (var pitch in pitches)
        {
            for (var p = 0; p < PlayersPerPitch; p++)
            {
                var (client, id) = await kit.PersonAsync(partner: false);
                players.Add((client, id, pitch));
            }
        }
        var day = PartnerKit.Day(5);

        using var start = new SemaphoreSlim(0);
        var rush = players.Select(async x =>
        {
            await start.WaitAsync();
            var r = await x.Client.PostAsync("/api/v1/bookings", new { pitchSlug = x.Pitch, date = day, hour = 20, duration = 1, acceptWaiver = true });
            return (x.Id, x.Pitch, r.Status, Code: r.Status == HttpStatusCode.Created ? null : r.Body.Code());
        }).ToList();
        start.Release(players.Count);
        var results = await Task.WhenAll(rush);

        // Every pitch: one winner, four told "slot_taken" (409)
        foreach (var group in results.GroupBy(r => r.Pitch))
        {
            group.Count(r => r.Status == HttpStatusCode.Created).ShouldBe(1, $"{group.Key}: {string.Join(", ", group.Select(r => $"{r.Status} {r.Code}"))}");
            group.Where(r => r.Status != HttpStatusCode.Created).ShouldAllBe(r => r.Status == HttpStatusCode.Conflict && r.Code == "slot_taken");
        }
        var winners = results.Where(r => r.Status == HttpStatusCode.Created).Select(r => r.Id).ToList();
        var losers = results.Where(r => r.Status != HttpStatusCode.Created).Select(r => r.Id).ToList();
        winners.Count.ShouldBe(Pitches);

        // The database: 20 confirmed bookings, one active hour per pitch, and money taken only from the 20 winners
        var (bookings, slots, payers) = await api.DbAsync(async db => (
            await db.Set<Booking>().Where(b => b.CustomerId != null && (winners.Contains(b.CustomerId.Value) || losers.Contains(b.CustomerId.Value))).ToListAsync(),
            await db.Set<BookingSlot>().CountAsync(s => s.IsActive && s.Hour == 20),
            await db.Set<Payment>().Where(p => p.Purpose == PaymentPurpose.Booking && p.Status == PaymentStatus.Paid && p.PayerUserId != null)
                .Select(p => p.PayerUserId!.Value).ToListAsync()));
        bookings.Count.ShouldBe(Pitches);
        bookings.ShouldAllBe(b => b.Status == BookingStatus.Confirmed && winners.Contains(b.CustomerId!.Value));
        slots.ShouldBe(Pitches);
        payers.Where(winners.Contains).Count().ShouldBe(Pitches);
        payers.Where(losers.Contains).ShouldBeEmpty();

        // Every winner has a payment receipt (unique numbers), and Super App's revenue wallet matches its ledger lines
        var ids = bookings.Select(b => b.Id).ToList();
        var (receipts, revenue, revenueLines) = await api.DbAsync(async db => (
            await db.Set<Receipt>().Where(r => r.Kind == ReceiptKind.Payment && ids.Contains(r.RefId) && winners.Contains(r.UserId!.Value))
                .Select(r => r.Number).ToListAsync(),
            await db.Set<Wallet>().SingleAsync(w => w.Type == WalletType.PlatformRevenue),
            await db.Set<LedgerEntry>().Where(l => l.Wallet.Type == WalletType.PlatformRevenue && l.Bucket == LedgerBucket.Available)
                .SumAsync(l => l.Amount)));
        receipts.Count.ShouldBe(Pitches);
        receipts.Distinct().Count().ShouldBe(Pitches);
        revenue.AvailableBalance.ShouldBe(revenueLines);
    }
}
