namespace SuperApp.Application.Abstractions.Persistence;

/// <summary>Saves everything the repositories added or changed, in one database transaction.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
