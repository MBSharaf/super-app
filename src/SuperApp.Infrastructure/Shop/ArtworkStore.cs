using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SkiaSharp;
using SuperApp.Application.Abstractions.Storage;
using SuperApp.Application.Features.Shop.Catalog;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Shop;

/// <summary>
/// Studio artwork on the server's disk (same root as other uploads, container "art", private). PNG and WEBP are saved
/// again as PNG at full size (transparency kept, hidden data removed), JPEG as JPEG; SVG is kept as text after a safety
/// check. Previews (max 1200 px PNG) are made once and kept next to the original.
/// </summary>
public sealed partial class ArtworkStore(AppDbContext db, IConfiguration configuration) : IArtworkStore
{
    private const int MaxBytes = 8 * 1024 * 1024;
    private const int MaxSide = 8000;
    private const int PreviewSide = 1200;
    private const string Container = "art";

    public async Task<(long FileId, int Width, int Height, bool IsVector)> SaveAsync(UploadedFile file, Guid ownerUserId, string field, CancellationToken ct)
    {
        if (file.Length <= 0 || file.Length > MaxBytes)
        {
            throw Invalid(field, "Artwork must be smaller than 8 MB.");
        }
        await using var buffer = new MemoryStream();
        await file.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (IsSvg(bytes))
        {
            EnsureSafeSvg(bytes, field);
            return (await StoreAsync(bytes, "image/svg+xml", ".svg", null, null, ownerUserId, file.FileName, ct), 0, 0, true);
        }
        using var bitmap = Sniff(bytes) is { } ? SKBitmap.Decode(bytes) : null;
        if (bitmap is null)
        {
            throw Invalid(field, "Upload a PNG, JPG, WEBP or SVG file.");
        }
        if (Math.Max(bitmap.Width, bitmap.Height) > MaxSide)
        {
            throw Invalid(field, $"The image is too big. Keep it under {MaxSide} pixels on each side.");
        }
        var isJpeg = Sniff(bytes) == "image/jpeg";
        using var image = SKImage.FromBitmap(bitmap);
        using var data = isJpeg ? image.Encode(SKEncodedImageFormat.Jpeg, 95) : image.Encode(SKEncodedImageFormat.Png, 100);
        var id = await StoreAsync(data.ToArray(), isJpeg ? "image/jpeg" : "image/png", isJpeg ? ".jpg" : ".png", bitmap.Width, bitmap.Height, ownerUserId, file.FileName, ct);
        return (id, bitmap.Width, bitmap.Height, false);
    }

    public async Task<ArtImage?> PreviewAsync(long fileId, CancellationToken ct)
    {
        var row = await RowAsync(fileId, ct);
        var path = row is null ? null : PathOf(row);
        if (row is null || !File.Exists(path))
        {
            return null;
        }
        if (row.ContentType == "image/svg+xml")
        {
            return new ArtImage(await File.ReadAllBytesAsync(path, ct), row.ContentType);
        }
        var preview = path + ".preview.png";
        if (!File.Exists(preview))
        {
            await File.WriteAllBytesAsync(preview, Resize(await File.ReadAllBytesAsync(path, ct)), ct);
        }
        return new ArtImage(await File.ReadAllBytesAsync(preview, ct), "image/png");
    }

    public async Task<ArtImage?> OriginalAsync(long fileId, CancellationToken ct)
    {
        var row = await RowAsync(fileId, ct);
        var path = row is null ? null : PathOf(row);
        return row is null || !File.Exists(path) ? null : new ArtImage(await File.ReadAllBytesAsync(path, ct), row.ContentType);
    }

    private Task<StoredFile?> RowAsync(long fileId, CancellationToken ct) =>
        db.Set<StoredFile>().AsNoTracking().FirstOrDefaultAsync(f => f.Id == fileId && f.Container == Container, ct);

    private async Task<long> StoreAsync(byte[] data, string contentType, string ext, int? width, int? height, Guid owner, string name, CancellationToken ct)
    {
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{ext}";
        var row = new StoredFile
        {
            OwnerUserId = owner, StorageKey = key, Container = Container, ContentType = contentType, SizeBytes = data.Length,
            FileName = SafeName(name) + ext, Width = width, Height = height, Sha256 = Convert.ToHexStringLower(SHA256.HashData(data)), IsPrivate = true,
        };
        var path = PathOf(row);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, data, ct);
        db.Set<StoredFile>().Add(row);
        await db.SaveChangesAsync(ct);
        return row.Id;
    }

    private static byte[] Resize(byte[] bytes)
    {
        using var original = SKBitmap.Decode(bytes);
        var scale = Math.Min(1f, (float)PreviewSide / Math.Max(original.Width, original.Height));
        using var resized = original.Resize(new SKImageInfo(Math.Max(1, (int)(original.Width * scale)), Math.Max(1, (int)(original.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear));
        using var image = SKImage.FromBitmap(resized);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    private static string? Sniff(byte[] b) => b switch
    {
        [0xFF, 0xD8, 0xFF, ..] => "image/jpeg",
        [0x89, 0x50, 0x4E, 0x47, ..] => "image/png",
        [0x52, 0x49, 0x46, 0x46, _, _, _, _, 0x57, 0x45, 0x42, 0x50, ..] => "image/webp",
        _ => null,
    };

    private static bool IsSvg(byte[] bytes)
    {
        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 1024)).TrimStart('﻿', ' ', '\r', '\n', '\t');
        return (head.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) || head.StartsWith("<svg", StringComparison.OrdinalIgnoreCase))
            && head.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Scripts, event handlers, embedded pages, entities and outside links are refused (the SVG is shown on our site).</summary>
    private static void EnsureSafeSvg(byte[] bytes, string field)
    {
        if (UnsafeSvg().IsMatch(Encoding.UTF8.GetString(bytes)))
        {
            throw Invalid(field, "This SVG contains scripts or links to other sites. Export it again as plain shapes, or upload a PNG.");
        }
    }

    [GeneratedRegex(@"<\s*script|<\s*foreignObject|<!ENTITY|<!DOCTYPE|javascript:|\son[a-z]+\s*=|(xlink:)?href\s*=\s*[""']\s*(https?:|//|data:text)", RegexOptions.IgnoreCase)]
    private static partial Regex UnsafeSvg();

    private string PathOf(StoredFile row) => Path.Combine(Path.GetFullPath(configuration["Storage:Local_Root"] ?? "storage"), row.Container, row.StorageKey);

    private static string SafeName(string name)
    {
        var stem = new string(Path.GetFileNameWithoutExtension(name).Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or ' ').Take(60).ToArray()).Trim();
        return stem.Length == 0 ? "art" : stem;
    }

    private static ValidationException Invalid(string field, string message) => new([new ValidationFailure(field, message)]);
}
