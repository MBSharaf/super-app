using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 2: every permission code (from <see cref="Permissions"/>) and the seeded roles with their permissions.
/// A seeded role gets its permissions when it is created; later only permission codes new to the database are added,
/// so a permission the CEO took away in the matrix stays away. Seeded roles are locked (no rename, no delete).
/// </summary>
public sealed class AccessSeeder(AppDbContext db, RoleManager<Role> roles) : ISeeder
{
    private sealed record RoleRow(string Name, string Description, RoleKind Kind, List<string> Permissions);

    public int Order => 2;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var known = await db.Set<Permission>().Select(p => p.Code).ToListAsync(ct);
        var added = await SeedPermissionsAsync(ct);
        foreach (var row in SeedFile.Read<List<RoleRow>>("Reference/roles.json"))
        {
            var (role, created) = await EnsureRoleAsync(row);
            var codes = Expand(row.Permissions);
            added += await GrantAsync(role, created ? codes : codes.Except(known).ToHashSet(), ct);
        }
        await db.SaveChangesAsync(ct);
        return added;
    }

    private async Task<int> SeedPermissionsAsync(CancellationToken ct)
    {
        var rows = Permissions.All.Select((p, i) => new Permission
        {
            Code = p.Code, Area = p.Area, NameEn = p.NameEn, NameAr = p.NameAr, IsSensitive = p.IsSensitive, SortOrder = i + 1,
        });
        var added = await db.Set<Permission>().AddMissingAsync(rows, p => p.Code, ct);
        await db.SaveChangesAsync(ct);
        return added;
    }

    /// <summary>Creates the role, or brings an older database's copy up to date (kind and lock).</summary>
    private async Task<(Role Role, bool Created)> EnsureRoleAsync(RoleRow row)
    {
        var role = await roles.FindByNameAsync(row.Name);
        if (role is not null)
        {
            if (role.Kind != row.Kind || !role.IsLocked)
            {
                (role.Kind, role.IsLocked) = (row.Kind, true);
                Check(row, await roles.UpdateAsync(role));
            }
            return (role, false);
        }
        role = new Role { Name = row.Name, Kind = row.Kind, DescriptionEn = row.Description, IsLocked = true };
        Check(row, await roles.CreateAsync(role));
        return (role, true);
    }

    private static void Check(RoleRow row, IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Role '{row.Name}': {string.Join("; ", result.Errors.Select(e => e.Description))}");
        }
    }

    private async Task<int> GrantAsync(Role role, HashSet<string> codes, CancellationToken ct)
    {
        var have = await db.Set<RolePermission>().Where(rp => rp.RoleId == role.Id).Select(rp => rp.Permission.Code).ToListAsync(ct);
        var missing = await db.Set<Permission>().Where(p => codes.Contains(p.Code) && !have.Contains(p.Code)).Select(p => p.Id).ToListAsync(ct);
        db.Set<RolePermission>().AddRange(missing.Select(id => new RolePermission { RoleId = role.Id, PermissionId = id }));
        return missing.Count;
    }

    /// <summary>"*" = every code, "content.*" = every code that starts with "content.".</summary>
    private static HashSet<string> Expand(IEnumerable<string> patterns) => patterns
        .SelectMany(p => p == "*" ? Permissions.All.Select(x => x.Code)
            : p.EndsWith(".*", StringComparison.Ordinal) ? Permissions.All.Select(x => x.Code).Where(c => c.StartsWith(p[..^1], StringComparison.Ordinal))
            : Permissions.Exists(p) ? [p] : throw new InvalidOperationException($"Unknown permission '{p}' in roles.json."))
        .ToHashSet();
}
