using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Money;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Payments;

public sealed class LoyaltyStore(AppDbContext db) : ILoyaltyStore, IPartnerCodeStore
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<int> BalanceAsync(Guid userId, CancellationToken ct) =>
        await db.Set<PointEntry>().Where(p => p.UserId == userId).SumAsync(p => (int?)p.Points, ct) ?? 0;

    public async Task<IReadOnlyList<PointRowDto>> HistoryAsync(Guid userId, CancellationToken ct) =>
        await db.Set<PointEntry>().Where(p => p.UserId == userId).OrderByDescending(p => p.Id).Take(100)
            .Select(p => new PointRowDto(p.Points, p.Kind.ToString(), p.Reason, p.CreatedAt)).ToListAsync(ct);

    public Task<bool> HasPointsForAsync(Guid userId, PointKind kind, string refType, long refId, CancellationToken ct) =>
        db.Set<PointEntry>().AnyAsync(p => p.UserId == userId && p.Kind == kind && p.RefType == refType && p.RefId == refId, ct);

    public void Add(PointEntry entry) => db.Set<PointEntry>().Add(entry);

    public void Add(Voucher voucher) => db.Set<Voucher>().Add(voucher);

    public void Add(PromoCodeUse use) => db.Set<PromoCodeUse>().Add(use);

    public async Task<string> NewVoucherCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "SP-" + new string(Enumerable.Range(0, 8).Select(_ => Letters[RandomNumberGenerator.GetInt32(Letters.Length)]).ToArray());
            if (!await db.Set<Voucher>().AnyAsync(v => v.Code == code, ct))
            {
                return code;
            }
        }
    }

    public async Task<IReadOnlyList<VoucherDto>> VouchersAsync(Guid userId, CancellationToken ct) =>
        await db.Set<Voucher>().Where(v => v.UserId == userId).OrderByDescending(v => v.Id)
            .Select(v => new VoucherDto(v.Code, v.Amount, v.Source.ToString(), v.Status.ToString(), v.ExpiresAt, v.CreatedAt)).ToListAsync(ct);

    public Task<Voucher?> VoucherAsync(string code, CancellationToken ct) => db.Set<Voucher>().FirstOrDefaultAsync(v => v.Code == code, ct);

    public Task<PromoCode?> PromoAsync(string code, CancellationToken ct) =>
        db.Set<PromoCode>().Include(p => p.Pitches).FirstOrDefaultAsync(p => p.Code == code, ct);

    public Task<int> UsesByAsync(long promoId, Guid userId, CancellationToken ct) =>
        db.Set<PromoCodeUse>().CountAsync(u => u.PromoCodeId == promoId && u.UserId == userId, ct);

    public async Task<decimal> VoucherOnBookingAsync(long bookingId, CancellationToken ct)
    {
        var face = await db.Set<Voucher>().Where(v => v.UsedOnType == "Booking" && v.UsedOnId == bookingId).SumAsync(v => (decimal?)v.Amount, ct) ?? 0;
        if (face == 0)
        {
            return 0;
        }
        // A voucher worth more than what was left to pay only covered the rest
        var b = await db.Set<SuperApp.Domain.Entities.Bookings.Booking>().Where(x => x.Id == bookingId).Select(x => new { x.DiscountAmount, x.PromoCodeId }).FirstAsync(ct);
        var promo = b.PromoCodeId is null ? 0 : await db.Set<PromoCodeUse>().Where(u => u.BookingId == bookingId).SumAsync(u => (decimal?)u.DiscountAmount, ct) ?? 0;
        return Math.Min(face, b.DiscountAmount - promo);
    }

    // Partner codes

    public async Task<IReadOnlyList<PromoCode>> MineAsync(Guid ownerUserId, CancellationToken ct) =>
        await db.Set<PromoCode>().Include(p => p.Pitches).Where(p => p.OwnerUserId == ownerUserId && p.Scope == CodeScope.Bookings).OrderByDescending(p => p.Id).ToListAsync(ct);

    public Task<PromoCode?> FindAsync(Guid ownerUserId, long id, CancellationToken ct) =>
        db.Set<PromoCode>().Include(p => p.Pitches).FirstOrDefaultAsync(p => p.Id == id && p.OwnerUserId == ownerUserId, ct);

    public Task<bool> CodeTakenAsync(string code, long? exceptId, CancellationToken ct) =>
        db.Set<PromoCode>().AnyAsync(p => p.Code == code && p.Id != exceptId, ct);

    public async Task<IReadOnlyList<long>> MyPitchIdsAsync(long partnerId, CancellationToken ct) =>
        await db.Set<Pitch>().Where(p => p.Venue.PartnerId == partnerId).Select(p => p.Id).ToListAsync(ct);

    public void Add(PromoCode code) => db.Set<PromoCode>().Add(code);
}
