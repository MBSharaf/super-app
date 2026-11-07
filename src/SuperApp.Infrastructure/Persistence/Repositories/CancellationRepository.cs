using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Cancellations;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>Cancellation requests (support.CancellationRequests): the customer's own, and the team's queue (oldest due first).</summary>
public sealed class CancellationRepository(AppDbContext db) : ICancellationRepository
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public void Add(CancellationRequest request) => db.Set<CancellationRequest>().Add(request);

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "CX-" + new string(Enumerable.Range(0, 6).Select(_ => Letters[RandomNumberGenerator.GetInt32(Letters.Length)]).ToArray());
            if (!await db.Set<CancellationRequest>().AnyAsync(r => r.Code == code, ct))
            {
                return code;
            }
        }
    }

    public Task<bool> OpenExistsAsync(CancellationKind kind, string reference, CancellationToken ct) =>
        db.Set<CancellationRequest>().AnyAsync(r => r.Kind == kind && r.Reference == reference && r.Status == CancellationStatus.Pending, ct);

    public Task<CancellationRequest?> FindAsync(string code, CancellationToken ct) =>
        db.Set<CancellationRequest>().FirstOrDefaultAsync(r => r.Code == code, ct);

    public async Task<IReadOnlyList<CancellationDto>> MineAsync(Guid customerId, CancellationToken ct) =>
        await Rows(db.Set<CancellationRequest>().Where(r => r.CustomerId == customerId).OrderByDescending(r => r.CreatedAt).Take(100), false).ToListAsync(ct);

    public async Task<PagedResult<CancellationDto>> ListAsync(string status, PageRequest page, CancellationToken ct)
    {
        var q = db.Set<CancellationRequest>().AsQueryable();
        q = status switch
        {
            "pending" => q.Where(r => r.Status == CancellationStatus.Pending).OrderBy(r => r.DueAt),
            "answered" => q.Where(r => r.Status != CancellationStatus.Pending).OrderByDescending(r => r.DecidedAt),
            _ => q.OrderByDescending(r => r.CreatedAt),
        };
        var total = await q.CountAsync(ct);
        var items = await Rows(q.Skip(page.Skip).Take(page.SafePageSize), true).ToListAsync(ct);
        return new PagedResult<CancellationDto>(items, page.SafePage, page.SafePageSize, total);
    }

    public Task<CancellationDto?> DtoAsync(string code, bool withCustomer, CancellationToken ct) =>
        Rows(db.Set<CancellationRequest>().Where(r => r.Code == code), withCustomer).FirstOrDefaultAsync(ct);

    public async Task<(string PitchName, IReadOnlyList<string> BookingCodes)?> PackageAsync(string code, Guid customerId, DateOnly today, CancellationToken ct)
    {
        var package = await db.Set<Package>().Where(p => p.Code == code && p.CustomerId == customerId)
            .Select(p => new { p.Id, PitchName = p.Pitch.Name }).FirstOrDefaultAsync(ct);
        if (package is null)
        {
            return null;
        }
        var codes = await db.Set<Booking>().Where(b => b.PackageId == package.Id && b.Status == BookingStatus.Confirmed && b.Date >= today)
            .OrderBy(b => b.Date).Select(b => b.Code).ToListAsync(ct);
        return (package.PitchName, codes);
    }

    private static IQueryable<CancellationDto> Rows(IQueryable<CancellationRequest> q, bool withCustomer) => q.Select(r => new CancellationDto(
        r.Code, r.Kind.ToString(), r.Reference, r.Label, Link(r.Kind, r.Reference), r.PaidOnline, r.RefundIfApproved, r.Refunded, r.Reason, r.Status.ToString(),
        r.DecisionNote, r.CreatedAt, r.DueAt, r.DecidedAt,
        withCustomer ? new CancellationCustomerDto(r.CustomerId, r.Customer.FullName, r.Customer.Email, r.Customer.PhoneNumber) : null));

    private static string Link(CancellationKind kind, string reference) => kind switch
    {
        CancellationKind.Booking => $"#/booking/{reference}",
        CancellationKind.Order => $"#/order/{reference}",
        CancellationKind.Game => $"#/players/{reference}",
        _ => "#/bookings",
    };
}
