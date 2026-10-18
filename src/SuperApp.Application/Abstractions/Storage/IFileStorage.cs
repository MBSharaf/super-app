namespace SuperApp.Application.Abstractions.Storage;

/// <summary>A file sent by the user, before any check (the controller turns IFormFile into this).</summary>
public sealed record UploadedFile(Stream Content, string FileName, string ContentType, long Length);

/// <summary>What a file is for; decides the allowed types and size.</summary>
public enum UploadKind
{
    /// <summary>ID card photo — private, JPEG/PNG/WEBP or PDF, up to 5 MB.</summary>
    IdDocument,

    /// <summary>Venue or product photo — public, JPEG/PNG/WEBP, up to 5 MB.</summary>
    Photo,

    /// <summary>Company paper (commercial register, tax card) — private, image or PDF, up to 8 MB.</summary>
    Document,
}

/// <summary>A stored file opened for download, with who may see it.</summary>
public sealed record FileDownload(Stream Content, string ContentType, string FileName, bool IsPrivate, Guid? OwnerUserId);

/// <summary>
/// Saves uploads safely: checks the real file type from its first bytes (not the name), the size, re-encodes
/// images (removes hidden data such as GPS), and records the file in ops.Files. Private files are only served
/// to the owner and to staff with the right permission.
/// </summary>
public interface IFileStorage
{
    Task<long> SaveAsync(UploadedFile file, UploadKind kind, Guid ownerUserId, string field, CancellationToken ct);

    Task<FileDownload?> OpenAsync(long fileId, CancellationToken ct);
}
