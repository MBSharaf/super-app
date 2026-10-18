using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Application.Features.Shop.Pod;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Design review queue, reports and strikes.</summary>
public sealed class PodAdminRepository(AppDbContext db) : IPodAdminRepository
{
    public async Task<PagedResult<ReviewDesignDto>> QueueAsync(int page, int pageSize, CancellationToken ct)
    {
        var q = db.Set<Design>().AsNoTracking().Where(d => d.Status == DesignStatus.InReview || (d.Status == DesignStatus.Live && d.SpotCheck));
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(d => d.Status == DesignStatus.Live ? 2 : d.Seller.Level.ReviewLane == ReviewLane.Priority ? 0 : 1).ThenBy(d => d.SubmittedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Include(d => d.Seller).ThenInclude(s => s.Store).Include(d => d.Seller).ThenInclude(s => s.Level).Include(d => d.CatalogProduct).Include(d => d.Category)
            .Include(d => d.Colors).ThenInclude(c => c.CatalogColor).Include(d => d.Tags).Include(d => d.FrontArtFile).Include(d => d.BackArtFile)
            .AsSplitQuery().ToListAsync(ct);
        var items = rows.Select(d => new ReviewDesignDto(d.Id, d.Title, d.Description, d.Seller.Store.Name, d.Seller.Store.Handle, d.Seller.Level.Key,
            d.Status == DesignStatus.Live ? "SpotCheck" : d.Seller.Level.ReviewLane == ReviewLane.Priority ? "Priority" : "Normal", d.CatalogProduct.NameEn, d.Category?.Key, d.Price,
            d.Colors.Select(c => c.CatalogColor.NameEn).ToList(), d.Tags.Select(t => t.Tag).ToList(),
            ShopQueries.Art(d.Id, "front", d.FrontArtFile?.Container, d.FrontArtFile?.StorageKey, d.FrontArtFileId),
            ShopQueries.Art(d.Id, "back", d.BackArtFile?.Container, d.BackArtFile?.StorageKey, d.BackArtFileId),
            d.FrontX is null ? null : new PlacementDto(d.FrontX.Value, d.FrontY ?? 0, d.FrontW ?? 0), d.BackX is null ? null : new PlacementDto(d.BackX.Value, d.BackY ?? 0, d.BackW ?? 0),
            d.Status.ToString(), d.SpotCheck, d.SubmittedAt)).ToList();
        return new PagedResult<ReviewDesignDto>(items, page, pageSize, total);
    }

    public Task<Design?> DesignAsync(long id, CancellationToken ct) =>
        db.Set<Design>().Include(d => d.Seller).ThenInclude(s => s.Store).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<PagedResult<DesignReportDto>> ReportsAsync(int page, int pageSize, CancellationToken ct)
    {
        var q = db.Set<DesignReport>().AsNoTracking().Where(r => r.Status == DesignReportStatus.Open);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).Select(r => new
        {
            r.Id, r.DesignId, r.Design.Title, Store = r.Design.Seller.Store.Name, r.Reason, r.Detail, r.Contact, r.CreatedAt,
            Open = db.Set<DesignReport>().Count(x => x.DesignId == r.DesignId && x.Status == DesignReportStatus.Open),
            ArtContainer = r.Design.FrontArtFile != null ? r.Design.FrontArtFile.Container : null, ArtKey = r.Design.FrontArtFile != null ? r.Design.FrontArtFile.StorageKey : null,
            r.Design.FrontArtFileId,
        }).ToListAsync(ct);
        var items = rows.Select(r => new DesignReportDto(r.Id, r.DesignId, r.Title, r.Store, r.Reason.ToString(), r.Detail, r.Contact, r.Open, r.CreatedAt,
            ShopQueries.Art(r.DesignId, "front", r.ArtContainer, r.ArtKey, r.FrontArtFileId))).ToList();
        return new PagedResult<DesignReportDto>(items, page, pageSize, total);
    }

    public Task<DesignReport?> ReportAsync(long id, CancellationToken ct) => db.Set<DesignReport>().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<DesignReport>> OpenReportsAsync(long designId, CancellationToken ct) =>
        await db.Set<DesignReport>().Where(r => r.DesignId == designId && r.Status == DesignReportStatus.Open).ToListAsync(ct);

    public void AddStrike(SellerStrike strike) => db.Set<SellerStrike>().Add(strike);

    public Task<int> ActiveStrikesAsync(long sellerId, DateTime now, CancellationToken ct) =>
        db.Set<SellerStrike>().CountAsync(s => s.SellerId == sellerId && s.RemovedAt == null && s.ExpiresAt > now, ct);
}
