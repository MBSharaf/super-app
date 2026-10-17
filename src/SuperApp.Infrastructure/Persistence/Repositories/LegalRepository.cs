using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Persistence;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Repositories;

public sealed class LegalRepository(AppDbContext db, IClock clock) : ILegalRepository
{
    public Task<long?> CurrentVersionIdAsync(string documentKey, CancellationToken ct) =>
        db.Set<LegalVersion>()
            .Where(v => v.PublishedAt != null && db.Set<LegalDocument>().Any(d => d.Id == v.DocumentId && d.Key == documentKey))
            .OrderByDescending(v => v.Version)
            .Select(v => (long?)v.Id)
            .FirstOrDefaultAsync(ct);

    public void AddAcceptance(long versionId, Guid userId, string context, string? ip) =>
        db.Set<LegalAcceptance>().Add(new LegalAcceptance { VersionId = versionId, UserId = userId, Context = context, Ip = ip, AcceptedAt = clock.UtcNow });
}

/// <summary>The unit of work is the DbContext itself: one SaveChanges, one transaction.</summary>
public sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
