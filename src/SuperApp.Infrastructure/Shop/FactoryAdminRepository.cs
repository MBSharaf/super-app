using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Factory;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;
using FactoryEntity = SuperApp.Domain.Entities.Factories.Factory;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Admin production view over every factory, and catalogue change approvals.</summary>
public sealed class FactoryAdminRepository(AppDbContext db) : IFactoryAdminRepository
{
    public async Task<IReadOnlyList<AdminFactoryDto>> FactoriesAsync(DateTime now, CancellationToken ct) =>
        await db.Set<FactoryEntity>().AsNoTracking().OrderBy(f => f.Name).Select(f => new AdminFactoryDto(f.Id, f.Name, f.OwnerUser.FullName, f.CapacityPerDay, f.SlaHours,
            f.IntakePaused, f.Status.ToString(),
            db.Set<Job>().Count(j => j.FactoryId == f.Id && j.Stage < JobStage.Ready),
            db.Set<Job>().Count(j => j.FactoryId == f.Id && j.Stage < JobStage.Ready && j.DueAt < now),
            db.Set<JobProblem>().Count(p => p.Job.FactoryId == f.Id && p.Status == JobProblemStatus.Open))).ToListAsync(ct);

    public Task<PagedResult<FactoryJobCardDto>> JobsAsync(AdminJobsQuery query, DateTime now, CancellationToken ct)
    {
        var q = db.Set<Job>().AsQueryable();
        if (query.FactoryId is { } id) { q = q.Where(j => j.FactoryId == id); }
        if (query.Problem) { q = q.Where(j => j.Problems.Any(p => p.Status == JobProblemStatus.Open)); }
        return FactoryRepository.JobPageAsync(q, "active", null, query.Late, null, query.Page, query.PageSize, now, ct);
    }

    public Task<Job?> JobAsync(string code, CancellationToken ct) => db.Set<Job>().Include(j => j.OrderItem).FirstOrDefaultAsync(j => j.Code == code, ct);

    public Task<JobProblem?> ProblemAsync(long id, CancellationToken ct) => db.Set<JobProblem>().FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<CatalogChangeRequest?> ChangeAsync(long id, CancellationToken ct) => db.Set<CatalogChangeRequest>().Include(c => c.Factory).FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<CatalogProduct?> CatalogForEditAsync(int id, CancellationToken ct) =>
        db.Set<CatalogProduct>().Include(c => c.Colors).Include(c => c.Sizes).Include(c => c.Details).AsSplitQuery().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<FactoryPrice?> PriceAsync(long factoryId, int catalogProductId, CancellationToken ct) =>
        db.Set<FactoryPrice>().FirstOrDefaultAsync(p => p.FactoryId == factoryId && p.CatalogProductId == catalogProductId, ct);

    public void AddPrice(FactoryPrice price) => db.Set<FactoryPrice>().Add(price);

    public async Task<IReadOnlyList<Guid>> SellersUsingAsync(int catalogProductId, CancellationToken ct) =>
        await db.Set<Design>().Where(d => d.CatalogProductId == catalogProductId && d.Status != DesignStatus.Removed).Select(d => d.Seller.UserId).Distinct().ToListAsync(ct);

    public async Task<Order> OrderOfJobAsync(Job job, CancellationToken ct) =>
        await db.Set<Order>().Include(o => o.Items).SingleAsync(o => o.Id == job.OrderItem.OrderId, ct);
}
