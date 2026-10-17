using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Features.Partners;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Repositories;

/// <summary>A partner's staff accounts.</summary>
public sealed class PartnerStaffRepository(AppDbContext db) : IPartnerStaffRepository
{
    public async Task<IReadOnlyList<PartnerStaffDto>> ListAsync(long partnerId, CancellationToken ct) =>
        await db.Set<PartnerStaffMember>().Where(s => s.PartnerId == partnerId).OrderBy(s => s.Role).ThenBy(s => s.User.FullName)
            .Select(s => new PartnerStaffDto(s.Id, s.UserId, s.User.FullName, s.User.Email ?? "", s.User.PhoneNumber, s.Role.ToString(), s.IsActive, s.CreatedAt))
            .ToListAsync(ct);

    public Task<PartnerStaffMember?> FindAsync(long partnerId, long id, CancellationToken ct) =>
        db.Set<PartnerStaffMember>().FirstOrDefaultAsync(s => s.Id == id && s.PartnerId == partnerId, ct);

    public async Task<bool> WorksForAPartnerAsync(Guid userId, CancellationToken ct) =>
        await db.Set<Partner>().AnyAsync(p => p.UserId == userId, ct) || await db.Set<PartnerStaffMember>().AnyAsync(s => s.UserId == userId, ct);

    public void Add(PartnerStaffMember member) => db.Set<PartnerStaffMember>().Add(member);

    public void Remove(PartnerStaffMember member) => db.Set<PartnerStaffMember>().Remove(member);
}
