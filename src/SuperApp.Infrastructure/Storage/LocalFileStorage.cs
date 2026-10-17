using System.Security.Cryptography;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Storage;

/// <summary>
/// Files on the server's disk (<c>STORAGE__LOCAL_ROOT</c>, default "storage"), recorded in ops.Files. The real type
/// is read from the first bytes; images are decoded and saved again as JPEG (max 2000 px), which removes hidden
/// data such as the GPS position of the photo. File names on disk are random, never the uploaded name.
/// </summary>
public sealed class LocalFileStorage(AppDbContext db, IConfiguration configuration) : IFileStorage
{
    private const int MaxSide = 2000;

    public async Task<long> SaveAsync(UploadedFile file, UploadKind kind, Guid ownerUserId, string field, CancellationToken ct)
    {
        var limit = kind == UploadKind.Document ? 8 * 1024 * 1024 : 5 * 1024 * 1024;
        if (file.Length <= 0 || file.Length > limit)
        {
            throw Invalid(field, $"Each file must be smaller than {limit / 1024 / 1024} MB.");
        }
        await using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var type = Sniff(bytes);
        var pdfAllowed = kind != UploadKind.Photo;
        if (type is null || (type == "application/pdf" && !pdfAllowed))
        {
            throw Invalid(field, pdfAllowed ? "Upload a photo (JPG, PNG or WEBP) or a PDF." : "Upload a photo (JPG, PNG or WEBP).");
        }
        var (data, contentType, width, height) = type == "application/pdf" ? (bytes, type, (int?)null, (int?)null) : Reencode(bytes, field);
        return await StoreAsync(data, contentType, width, height, kind, ownerUserId, file.FileName, ct);
    }

    public async Task<FileDownload?> OpenAsync(long fileId, CancellationToken ct)
    {
        var row = await db.Set<StoredFile>().FirstOrDefaultAsync(f => f.Id == fileId, ct);
        var path = row is null ? null : Path.Combine(Root(), row.Container, row.StorageKey);
        return path is null || !File.Exists(path) ? null : new FileDownload(File.OpenRead(path), row!.ContentType, row.FileName, row.IsPrivate, row.OwnerUserId);
    }

    private async Task<long> StoreAsync(byte[] data, string contentType, int? width, int? height, UploadKind kind, Guid owner, string originalName, CancellationToken ct)
    {
        var container = kind == UploadKind.Photo ? "public" : "private";
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{(contentType == "application/pdf" ? ".pdf" : ".jpg")}";
        var path = Path.Combine(Root(), container, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, data, ct);
        var row = new StoredFile
        {
            OwnerUserId = owner, StorageKey = key, Container = container, ContentType = contentType, SizeBytes = data.Length,
            FileName = SafeName(originalName, contentType), Width = width, Height = height, Sha256 = Convert.ToHexStringLower(SHA256.HashData(data)),
            IsPrivate = kind != UploadKind.Photo,
        };
        db.Set<StoredFile>().Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    /// <summary>The real type from the file's first bytes; null for anything that is not an allowed type.</summary>
    private static string? Sniff(byte[] b) => b switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        [0x25, 0x50, 0x44, 0x46, ..] => "application/pdf",
        _ => null,
    };

    private static (byte[] Data, string ContentType, int? Width, int? Height) Reencode(byte[] bytes, string field)
    {
        using var original = SKBitmap.Decode(bytes) ?? throw Invalid(field, "This image could not be read. Try another photo.");
        var scale = Math.Min(1f, (float)MaxSide / Math.Max(original.Width, original.Height));
        using var resized = scale < 1f
            ? original.Resize(new SKImageInfo((int)(original.Width * scale), (int)(original.Height * scale)), new SKSamplingOptions(SKFilterMode.Linear))
            : original.Copy();
        using var image = SKImage.FromBitmap(resized);
        using var jpeg = image.Encode(SKEncodedImageFormat.Jpeg, 85);
        return (jpeg.ToArray(), "image/jpeg", resized.Width, resized.Height);
    }

    private static string SafeName(string name, string contentType)
    {
        var stem = Path.GetFileNameWithoutExtension(name);
        stem = new string(stem.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').Take(60).ToArray()).Trim();
        return (stem.Length == 0 ? "file" : stem) + (contentType == "application/pdf" ? ".pdf" : ".jpg");
    }

    private string Root() => Path.GetFullPath(configuration["Storage:Local_Root"] ?? "storage");

    private static ValidationException Invalid(string field, string message) => new([new ValidationFailure(field, message)]);
}
