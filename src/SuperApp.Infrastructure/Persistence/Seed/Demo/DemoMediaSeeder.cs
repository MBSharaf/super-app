using System.Security.Cryptography;
using System.Text;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Demo 12: file records for the demo photos (Unsplash / Pexels ids the site already uses) and the demo design art.
/// They point to external pictures, so size and hash describe the key, not real bytes; they are removed with the
/// rest of the demo data before launch.
/// </summary>
public sealed class DemoMediaSeeder(AppDbContext db) : ISeeder
{
    private sealed record MediaRow(string Key, string Container, string ContentType);

    public int Order => 12;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var rows = SeedFile.Read<List<MediaRow>>("Demo/media.json").Select(m => new StoredFile
        {
            StorageKey = m.Key,
            Container = m.Container,
            FileName = m.Key.Replace(':', '-') + (m.ContentType == "image/svg+xml" ? ".svg" : ".jpg"),
            ContentType = m.ContentType,
            SizeBytes = 1,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(m.Key))),
        });
        var added = await db.Set<StoredFile>().AddMissingAsync(rows, f => f.StorageKey, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }
}
