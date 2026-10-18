using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Shop.Factory;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;
using FactoryEntity = SuperApp.Domain.Entities.Factories.Factory;

namespace SuperApp.Infrastructure.Shop;

/// <summary>Factories, staff, devices and catalogue change requests. Jobs and reports are in the other parts of this class.</summary>
public sealed partial class FactoryRepository(AppDbContext db, IFactoryTokens tokens) : IFactoryRepository
{
    public async Task<FactoryScope?> OwnerScopeAsync(Guid ownerUserId, CancellationToken ct)
    {
        var f = await db.Set<FactoryEntity>().AsNoTracking().Where(x => x.OwnerUserId == ownerUserId)
            .Select(x => new { x.Id, x.Name, x.Status, x.IntakePaused, x.SlaHours, Owner = x.OwnerUser.FullName }).FirstOrDefaultAsync(ct);
        return f is null ? null : new FactoryScope(f.Id, f.Name, ownerUserId, null, f.Owner, FactoryRole.Owner, f.Status, f.IntakePaused, f.SlaHours);
    }

    public async Task<FactoryScope?> StaffScopeAsync(long staffId, long factoryId, CancellationToken ct)
    {
        var s = await db.Set<FactoryStaffMember>().AsNoTracking().Where(x => x.Id == staffId && x.FactoryId == factoryId && x.IsActive)
            .Select(x => new { x.Id, x.Name, x.Role, x.Factory.OwnerUserId, x.Factory.Status, x.Factory.IntakePaused, x.Factory.SlaHours, FactoryName = x.Factory.Name })
            .FirstOrDefaultAsync(ct);
        return s is null ? null : new FactoryScope(factoryId, s.FactoryName, s.OwnerUserId, s.Id, s.Name, s.Role, s.Status, s.IntakePaused, s.SlaHours);
    }

    public async Task<FactoryDevice?> DeviceAsync(string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 100)
        {
            return null;
        }
        var hash = tokens.Hash(token);
        var device = await db.Set<FactoryDevice>().FirstOrDefaultAsync(d => d.TokenHash == hash && d.RevokedAt == null, ct);
        if (device is not null && (device.LastSeenAt is null || device.LastSeenAt < DateTime.UtcNow.AddMinutes(-5)))
        {
            await db.Set<FactoryDevice>().Where(d => d.Id == device.Id).ExecuteUpdateAsync(u => u.SetProperty(d => d.LastSeenAt, DateTime.UtcNow), ct);
        }
        return device;
    }

    public void AddDevice(FactoryDevice device) => db.Set<FactoryDevice>().Add(device);

    public Task<FactoryEntity?> FactoryAsync(long id, CancellationToken ct) => db.Set<FactoryEntity>().FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<StaffNameDto>> StaffNamesAsync(long factoryId, CancellationToken ct) =>
        await db.Set<FactoryStaffMember>().AsNoTracking().Where(s => s.FactoryId == factoryId && s.IsActive).OrderBy(s => s.Role).ThenBy(s => s.Name)
            .Select(s => new StaffNameDto(s.Id, s.Name, s.Role.ToString())).ToListAsync(ct);

    public Task<FactoryStaffMember?> StaffAsync(long factoryId, long staffId, CancellationToken ct) =>
        db.Set<FactoryStaffMember>().FirstOrDefaultAsync(s => s.FactoryId == factoryId && s.Id == staffId, ct);

    public async Task<IReadOnlyList<StaffDto>> TeamAsync(long factoryId, DateTime now, CancellationToken ct) =>
        await db.Set<FactoryStaffMember>().AsNoTracking().Where(s => s.FactoryId == factoryId).OrderBy(s => s.Role).ThenBy(s => s.Name)
            .Select(s => new StaffDto(s.Id, s.Name, s.Role.ToString(), s.IsActive, s.LockedUntil > now, s.CreatedAt)).ToListAsync(ct);

    public Task<bool> StaffNameTakenAsync(long factoryId, string name, long? exceptId, CancellationToken ct) =>
        db.Set<FactoryStaffMember>().AnyAsync(s => s.FactoryId == factoryId && s.Name == name && s.Id != exceptId, ct);

    public void AddStaff(FactoryStaffMember staff) => db.Set<FactoryStaffMember>().Add(staff);

    public void AddProblem(JobProblem problem) => db.Set<JobProblem>().Add(problem);

    public Task<string> CustomerNameAsync(Guid customerId, CancellationToken ct) =>
        db.Users.IgnoreQueryFilters().Where(u => u.Id == customerId).Select(u => u.FullName).SingleAsync(ct);

    public Task<bool> HasFactoryShipmentAsync(long orderId, CancellationToken ct) =>
        db.Set<Shipment>().AnyAsync(s => s.OrderId == orderId && s.FactoryId != null && !s.IsReturn && s.Status != ShipmentStatus.Cancelled, ct);

    public Task<CatalogProduct?> CatalogProductAsync(int id, CancellationToken ct) => db.Set<CatalogProduct>().AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> PendingChangeAsync(long factoryId, int catalogProductId, CatalogChangeKind kind, CancellationToken ct) =>
        db.Set<CatalogChangeRequest>().AnyAsync(c => c.FactoryId == factoryId && c.CatalogProductId == catalogProductId && c.Kind == kind && c.Status == ChangeRequestStatus.Pending, ct);

    public void AddChange(CatalogChangeRequest change) => db.Set<CatalogChangeRequest>().Add(change);

    public async Task<IReadOnlyList<CatalogueChangeDto>> ChangesAsync(long? factoryId, bool pendingOnly, CancellationToken ct) =>
        await db.Set<CatalogChangeRequest>().AsNoTracking().Where(c => (factoryId == null || c.FactoryId == factoryId) && (!pendingOnly || c.Status == ChangeRequestStatus.Pending))
            .OrderByDescending(c => c.Id)
            .Select(c => new CatalogueChangeDto(c.Id, c.CatalogProductId, c.CatalogProduct.NameEn, c.Factory.Name, c.Kind.ToString(), c.ChangeJson, c.Reason, c.Status.ToString(),
                c.ReviewNote, c.CreatedAt, c.ReviewedAt)).ToListAsync(ct);
}
