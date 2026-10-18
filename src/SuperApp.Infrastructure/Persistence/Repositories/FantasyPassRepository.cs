using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Fantasy;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Fantasy Pass rows, prices (setting fantasy.passPrice), the seller-level discount and the payments switch.</summary>
public sealed class FantasyPassRepository(AppDbContext db) : IFantasyPassRepository
{
    public async Task<IReadOnlyList<(int Id, string Key)>> CompetitionsAsync(CancellationToken ct) =>
        (await db.Set<Competition>().AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder).Select(c => new { c.Id, c.Key }).ToListAsync(ct))
            .Select(c => (c.Id, c.Key)).ToList();

    public async Task<IReadOnlyList<FantasyPass>> ActiveAsync(Guid userId, DateTime now, CancellationToken ct) =>
        await db.Set<FantasyPass>().AsNoTracking().Where(p => p.UserId == userId && p.EndsAt > now).OrderBy(p => p.StartsAt).ToListAsync(ct);

    public Task<FantasyPass?> RecentAsync(Guid userId, int? competitionId, DateTime since, CancellationToken ct) =>
        db.Set<FantasyPass>().AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.CompetitionId == competitionId && p.CreatedAt >= since, ct);

    public void Add(FantasyPass pass) => db.Set<FantasyPass>().Add(pass);

    public async Task<(decimal One, decimal Both)> PricesAsync(CancellationToken ct)
    {
        var json = await db.Set<Setting>().Where(s => s.Key == "fantasy.passPrice").Select(s => s.Value).FirstOrDefaultAsync(ct);
        if (json is null)
        {
            return (50, 80);
        }
        var root = JsonDocument.Parse(json).RootElement;
        if (root.TryGetProperty("value", out var inner) && inner.ValueKind == JsonValueKind.Object)
        {
            root = inner;
        }
        decimal Read(string name, decimal fallback) => root.TryGetProperty(name, out var v) && v.TryGetDecimal(out var d) ? d : fallback;
        return (Read("one", 50), Read("both", 80));
    }

    public async Task<decimal> SellerDiscountAsync(Guid userId, CancellationToken ct) =>
        await db.Set<Seller>().Where(s => s.UserId == userId && s.Status == SellerStatus.Active).Select(s => (decimal?)s.Level.FantasyDiscount).FirstOrDefaultAsync(ct) ?? 0;

    public Task<bool> PaymentsLiveAsync(CancellationToken ct) =>
        db.Set<Integration>().AnyAsync(i => i.Category == IntegrationCategory.Payment && i.IsPrimary && i.IsEnabled && i.Mode == IntegrationMode.Live, ct);
}

/// <summary>Sponsors (soft-deleted, so past prizes keep their sponsor).</summary>
public sealed class SponsorRepository(AppDbContext db) : ISponsorRepository
{
    public async Task<IReadOnlyList<SponsorDto>> ListAsync(CancellationToken ct) =>
        await db.Set<Sponsor>().AsNoTracking().OrderBy(s => s.Tier).ThenBy(s => s.Name)
            .Select(s => new SponsorDto(s.Id, s.Name, s.Tier.ToString(), s.ContactEmail, s.ContractValue, s.StartsOn, s.EndsOn, s.Color)).ToListAsync(ct);

    public Task<Sponsor?> FindAsync(long id, CancellationToken ct) => db.Set<Sponsor>().FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<bool> NameTakenAsync(string name, long? exceptId, CancellationToken ct) =>
        db.Set<Sponsor>().AnyAsync(s => s.Name == name && s.Id != exceptId, ct);

    public void Add(Sponsor sponsor) => db.Set<Sponsor>().Add(sponsor);
}

/// <summary>
/// Prize vouchers written to money.Vouchers (source "fantasy prize", 90 days). Phase 5 seam: the money window may
/// replace this with its voucher service; the code prefix FP{groupId}- marks the group they were won in.
/// </summary>
public sealed class PrizeVouchers(AppDbContext db, IClock clock) : IPrizeVouchers
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public Task<bool> GivenAsync(string codePrefix, CancellationToken ct) =>
        db.Set<Voucher>().AnyAsync(v => v.Source == VoucherSource.FantasyPrize && v.Code.StartsWith(codePrefix), ct);

    public async Task<string> IssueAsync(Guid userId, decimal amount, string codePrefix, CancellationToken ct)
    {
        var code = codePrefix + new string(Enumerable.Range(0, Math.Max(4, 20 - codePrefix.Length)).Select(_ => Letters[RandomNumberGenerator.GetInt32(Letters.Length)]).ToArray());
        db.Set<Voucher>().Add(new Voucher
        {
            Code = code[..Math.Min(20, code.Length)], UserId = userId, Amount = amount, Source = VoucherSource.FantasyPrize,
            Status = VoucherStatus.Active, ExpiresAt = clock.UtcNow.AddDays(90),
        });
        await db.SaveChangesAsync(ct);
        return code[..Math.Min(20, code.Length)];
    }
}
