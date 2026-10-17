namespace SuperApp.Application.Abstractions.Persistence;

/// <summary>Legal documents: the published version of a document and who accepted it.</summary>
public interface ILegalRepository
{
    /// <summary>Id of the latest published version of a document (for example "terms"); null when none is published.</summary>
    Task<long?> CurrentVersionIdAsync(string documentKey, CancellationToken ct);

    /// <summary>Records that a person accepted a version (saved with the next unit of work).</summary>
    void AddAcceptance(long versionId, Guid userId, string context, string? ip);
}
