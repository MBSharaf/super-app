using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Turns the keys used in the demo seed files (account keys such as "acc-o1", photo keys, store handles) into
/// database ids. People are matched by email, files by storage key — the natural keys.
/// </summary>
internal static class DemoRefs
{
    internal sealed record PersonRow(string Key, string Name, string Email, string AccountType, List<string> Roles);
    internal sealed record LimitRow(string User, decimal Limit, bool SecondApprove);
    internal sealed record PeopleFile(List<PersonRow> People, List<LimitRow> StaffLimits);

    internal static PeopleFile People() => SeedFile.Read<PeopleFile>("Demo/people.json");

    /// <summary>Account key → user id, for the demo people that exist in the database.</summary>
    internal static async Task<Dictionary<string, Guid>> UsersAsync(AppDbContext db, CancellationToken ct)
    {
        var people = People().People;
        var emails = people.Select(p => p.Email.ToUpperInvariant()).ToList();
        var ids = await db.Users.Where(u => emails.Contains(u.NormalizedEmail!)).ToDictionaryAsync(u => u.NormalizedEmail!, u => u.Id, ct);
        return people.Where(p => ids.ContainsKey(p.Email.ToUpperInvariant())).ToDictionary(p => p.Key, p => ids[p.Email.ToUpperInvariant()]);
    }

    /// <summary>Storage key → file id.</summary>
    internal static Task<Dictionary<string, long>> FilesAsync(AppDbContext db, CancellationToken ct) =>
        db.Set<StoredFile>().ToDictionaryAsync(f => f.StorageKey, f => f.Id, ct);

    internal static T Parse<T>(string value) where T : struct, Enum => Enum.Parse<T>(value, ignoreCase: true);
}
