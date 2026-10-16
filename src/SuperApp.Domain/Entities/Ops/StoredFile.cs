using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>An uploaded or generated file; the bytes live in storage, not in the database. Table <c>ops.Files</c>.</summary>
public class StoredFile : BaseEntity, ISoftDelete
{
    public Guid? OwnerUserId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string Container { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool IsPrivate { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    public User? OwnerUser { get; set; }
}
