using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Responses;
using SuperApp.Application.Features.Applications.Dtos;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Repositories;

public sealed class ApplicationRepository(AppDbContext db, IDataProtectionProvider protection, IClock clock) : IApplicationRepository
{
    /// <summary>National ID numbers are encrypted with this purpose (data-protection keys on disk).</summary>
    public const string KycPurpose = "SuperApp.Kyc.NationalId";
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public async Task<string> NewCodeAsync(CancellationToken ct)
    {
        while (true)
        {
            var code = "APP-" + new string(Enumerable.Range(0, 6).Select(_ => CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]).ToArray());
            if (!await db.Set<PartnerApplication>().AnyAsync(a => a.Code == code, ct))
            {
                return code;
            }
        }
    }

    public void Add(PartnerApplication application) => db.Set<PartnerApplication>().Add(application);

    public Task<PartnerApplication?> FindAsync(string code, CancellationToken ct) =>
        db.Set<PartnerApplication>().Include(a => a.Files).FirstOrDefaultAsync(a => a.Code == code, ct);

    public async Task<IReadOnlyList<PartnerApplication>> MineAsync(Guid applicantId, CancellationToken ct) =>
        await db.Set<PartnerApplication>().Include(a => a.Files).Where(a => a.ApplicantId == applicantId)
            .OrderByDescending(a => a.CreatedAt).AsNoTracking().ToListAsync(ct);

    public async Task<PagedResult<PartnerApplication>> ListAsync(IReadOnlyCollection<ApplicationType> types, ApplicationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        var query = db.Set<PartnerApplication>().Where(a => types.Contains(a.Type));
        if (status is not null)
        {
            query = query.Where(a => a.Status == status);
        }
        var total = await query.CountAsync(ct);
        var items = await query.Include(a => a.Files).OrderBy(a => a.Status).ThenBy(a => a.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking().ToListAsync(ct);
        return new PagedResult<PartnerApplication>(items, page, pageSize, total);
    }

    public Task<bool> HasOpenAsync(Guid applicantId, ApplicationType type, CancellationToken ct) =>
        db.Set<PartnerApplication>().AnyAsync(a => a.ApplicantId == applicantId && a.Type == type && a.Status == ApplicationStatus.Pending, ct);

    public Task<bool> IsAlreadyMemberAsync(Guid applicantId, ApplicationType type, CancellationToken ct) => type switch
    {
        ApplicationType.PitchOwner => db.Set<Partner>().AnyAsync(p => p.UserId == applicantId, ct),
        ApplicationType.Seller => db.Set<Seller>().AnyAsync(s => s.UserId == applicantId, ct),
        _ => db.Set<Factory>().AnyAsync(f => f.OwnerUserId == applicantId, ct),
    };

    public Task<ApplicantResponse> ApplicantAsync(Guid userId, CancellationToken ct) =>
        db.Users.IgnoreQueryFilters().Where(u => u.Id == userId)
            .Select(u => new ApplicantResponse(u.Id, u.FullName, u.Email!, u.PhoneNumber)).SingleAsync(ct);

    public Task<bool> AreaExistsAsync(int areaId, CancellationToken ct) => db.Set<Area>().AnyAsync(a => a.Id == areaId && a.IsActive, ct);

    public Task<bool> StoreHandleTakenAsync(string handle, CancellationToken ct) => db.Set<Store>().IgnoreQueryFilters().AnyAsync(s => s.Handle == handle, ct);

    public Task<bool> FactoryNameTakenAsync(string name, CancellationToken ct) => db.Set<Factory>().AnyAsync(f => f.Name == name, ct);

    public async Task SaveKycAsync(Guid userId, string nationalId, long frontFileId, long backFileId, CancellationToken ct)
    {
        var row = await db.Set<KycDocument>().FirstOrDefaultAsync(k => k.UserId == userId, ct);
        if (row is null)
        {
            row = new KycDocument { UserId = userId };
            db.Set<KycDocument>().Add(row);
        }
        row.NationalId = protection.CreateProtector(KycPurpose).Protect(nationalId);
        (row.FrontFileId, row.BackFileId, row.Status, row.RejectReason, row.ReviewedAt, row.ReviewedById) = (frontFileId, backFileId, KycStatus.Pending, null, null, null);
        await db.SaveChangesAsync(ct);
    }

    public async Task DecideKycAsync(Guid userId, bool verified, Guid reviewerId, string? reason, CancellationToken ct)
    {
        var row = await db.Set<KycDocument>().FirstOrDefaultAsync(k => k.UserId == userId, ct);
        if (row is null)
        {
            return;
        }
        row.Status = verified ? KycStatus.Verified : KycStatus.Rejected;
        row.RejectReason = verified ? null : reason;
        row.ReviewedById = reviewerId;
        row.ReviewedAt = clock.UtcNow;
    }
}
