using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence.Seed.Reference;

namespace SuperApp.Infrastructure.Persistence.Seed.Demo;

/// <summary>
/// Demo 11: the fictional people of today's site — partners, sellers, the factory owner, admin HQ, the CFO, customers,
/// team captains and the 18 AI agents' service accounts — with their roles and the staff money limits.
/// Demo accounts have no password: nobody can sign in as them (admins open their views with "open as").
/// </summary>
public sealed class DemoPeopleSeeder(AppDbContext db, UserManager<User> users) : ISeeder
{
    public int Order => 11;
    public bool IsDemo => true;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        var file = DemoRefs.People();
        var added = 0;
        foreach (var person in file.People)
        {
            added += await EnsurePersonAsync(person) ? 1 : 0;
        }
        var ids = await DemoRefs.UsersAsync(db, ct);
        var limits = file.StaffLimits.Select(l => new StaffLimit
        {
            UserId = ids[l.User], RefundLimit = Math.Min(l.Limit, 1000), CreditLimit = Math.Min(l.Limit, 1000), PayoutLimit = l.Limit, CanSecondApprove = l.SecondApprove,
        });
        added += await db.Set<StaffLimit>().AddMissingAsync(limits, l => l.UserId, ct);
        await db.SaveChangesAsync(ct);
        await AssignHumanPositionsAsync(ids, ct);
        return added;
    }

    private async Task<bool> EnsurePersonAsync(DemoRefs.PersonRow person)
    {
        if (await users.FindByEmailAsync(person.Email) is not null)
        {
            return false;
        }
        var user = new User
        {
            UserName = person.Email, Email = person.Email, EmailConfirmed = true, FullName = person.Name,
            AccountType = DemoRefs.Parse<AccountType>(person.AccountType), Status = UserStatus.Active,
        };
        Check(await users.CreateAsync(user), person.Email);
        Check(await users.AddToRolesAsync(user, person.Roles), person.Email);
        return true;
    }

    /// <summary>Admin HQ holds the admin manager position and the CFO the finance director position.</summary>
    private async Task AssignHumanPositionsAsync(Dictionary<string, Guid> ids, CancellationToken ct)
    {
        var titles = OrgSeeder.Load().Positions.ToDictionary(p => p.Key, p => p.En);
        foreach (var (account, position) in new[] { ("acc-admin", "hq"), ("acc-cfo", "cfo") })
        {
            var row = await db.Set<OrgPosition>().FirstOrDefaultAsync(p => p.TitleEn == titles[position], ct);
            if (row is not null && row.HolderUserId is null && ids.TryGetValue(account, out var userId))
            {
                row.HolderUserId = userId;
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static void Check(IdentityResult result, string email)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Demo account {email}: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
