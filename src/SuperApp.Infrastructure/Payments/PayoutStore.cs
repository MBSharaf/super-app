using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Money;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Payments;

public sealed class PayoutStore(AppDbContext db) : IPayoutStore
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private static readonly PayoutStatus[] Open = [PayoutStatus.Requested, PayoutStatus.Prepared, PayoutStatus.WaitingForApproval];

    /// <summary>Partners withdraw from their bookings wallet (sellers and factories when the store returns).</summary>
    public async Task<PayeeInfo?> PayeeAsync(Guid userId, WalletType type, CancellationToken ct) => type != WalletType.PartnerBookings ? null :
        await db.Set<Partner>().Where(p => p.UserId == userId)
            .Select(p => new PayeeInfo(p.UserId, p.BusinessName, p.PayoutMethod, p.PayoutDetail, p.Status == PartnerStatus.Active)).FirstOrDefaultAsync(ct);

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "WD-" + new string(Enumerable.Range(0, 8).Select(_ => Letters[RandomNumberGenerator.GetInt32(Letters.Length)]).ToArray());
            if (!await db.Set<PayoutRequest>().AnyAsync(p => p.Code == code, ct))
            {
                return code;
            }
        }
    }

    public void Add(PayoutRequest payout) => db.Set<PayoutRequest>().Add(payout);

    public void Add(Approval approval) => db.Set<Approval>().Add(approval);

    public Task<bool> HasOpenAsync(long walletId, CancellationToken ct) =>
        db.Set<PayoutRequest>().AnyAsync(p => p.WalletId == walletId && Open.Contains(p.Status), ct);

    public Task<PayoutRequest?> FindAsync(string code, CancellationToken ct) =>
        db.Set<PayoutRequest>().Include(p => p.Wallet).FirstOrDefaultAsync(p => p.Code == code, ct);

    public Task<PayoutRequest?> FindByIdAsync(long id, CancellationToken ct) =>
        db.Set<PayoutRequest>().Include(p => p.Wallet).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PayoutDto>> MineAsync(Guid userId, CancellationToken ct) =>
        await Rows(db.Set<PayoutRequest>().Where(p => p.Wallet.OwnerUserId == userId)).Take(100).ToListAsync(ct);

    public async Task<PagedResult<PayoutDto>> ListAsync(PayoutStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var all = db.Set<PayoutRequest>().Where(p => status == null || p.Status == status);
        var items = await Rows(all).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<PayoutDto>(items, page, pageSize, await all.CountAsync(ct));
    }

    public Task<Approval?> ApprovalAsync(long id, CancellationToken ct) =>
        db.Set<Approval>().Include(a => a.Decisions).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ApprovalDto>> ApprovalsAsync(ApprovalStatus? status, Guid me, CancellationToken ct)
    {
        var query = db.Set<Approval>().AsNoTracking().Include(a => a.RequestedBy).Include(a => a.Decisions).ThenInclude(d => d.Decider)
            .Where(a => status == null || a.Status == status);
        var rows = await (status == ApprovalStatus.Waiting ? query.OrderBy(a => a.CreatedAt) : query.OrderByDescending(a => a.CreatedAt)).Take(200).ToListAsync(ct);
        var ids = rows.Where(a => a.RefType == "Payout").Select(a => a.RefId).ToList();
        var codes = await db.Set<PayoutRequest>().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, ct);
        return rows.Select(a => new ApprovalDto(a.Id, a.Kind.ToString(), a.RefType, a.RefType == "Payout" ? codes.GetValueOrDefault(a.RefId) : null,
            a.Amount, a.Summary, a.Status.ToString(), a.CreatedAt, a.RequestedBy.FullName, a.RequestedById == me, a.DecidedAt,
            a.Decisions.OrderBy(d => d.CreatedAt).Select(d => new ApprovalDecisionDto(d.Decider.FullName, d.Decision.ToString(), d.Note, d.CreatedAt)).ToList())).ToList();
    }

    public Task<decimal?> AgentMoneyLimitAsync(long agentId, CancellationToken ct) =>
        db.Set<Agent>().Where(a => a.Id == agentId).Select(a => (decimal?)a.MoneyLimit).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<LedgerRowDto>> StatementAsync(Guid userId, WalletType type, CancellationToken ct) =>
        await db.Set<LedgerEntry>().Where(e => e.Wallet.OwnerUserId == userId && e.Wallet.Type == type).OrderBy(e => e.Id).Take(10_000)
            .Select(e => new LedgerRowDto(e.Id, e.Kind.ToString(), e.Bucket.ToString(), e.Amount, e.RefType, e.RefId, e.AvailableAt, e.CreatedAt)).ToListAsync(ct);

    private IQueryable<PayoutDto> Rows(IQueryable<PayoutRequest> payouts) =>
        from p in payouts
        join u in db.Set<User>() on p.Wallet.OwnerUserId equals u.Id
        orderby p.Id descending
        select new PayoutDto(p.Code, p.Wallet.Type.ToString(), p.Amount, p.Method.ToString(), p.Destination, p.Status.ToString(),
            p.TransferRef, p.RejectReason, p.CreatedAt, p.SentAt, u.FullName, p.ApprovalId);
}
