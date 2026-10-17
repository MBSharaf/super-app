using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Seed.Reference;

/// <summary>
/// Seed 3: the CEO account. Email, name and password come only from .env (PRD FR-AUTH-06); without them the step
/// is skipped with a warning. Two-factor sign-in is set up by the CEO at the first sign-in.
/// </summary>
public sealed class CeoAccountSeeder(UserManager<User> users, SeedOptions options, ILogger<CeoAccountSeeder> logger) : ISeeder
{
    public int Order => 3;

    public async Task<int> SeedAsync(CancellationToken ct)
    {
        if (options.CeoEmail is null || options.CeoPassword is null)
        {
            logger.LogWarning("CEO account not created: set SEED__CEO_EMAIL and SEED__CEO_PASSWORD in .env, then run the seed again.");
            return 0;
        }
        if (await users.FindByEmailAsync(options.CeoEmail) is not null)
        {
            return 0;
        }
        var ceo = new User
        {
            UserName = options.CeoEmail,
            Email = options.CeoEmail,
            EmailConfirmed = true,
            FullName = options.CeoName,
            AccountType = AccountType.Person,
            Status = UserStatus.Active,
        };
        Check(await users.CreateAsync(ceo, options.CeoPassword));
        Check(await users.AddToRoleAsync(ceo, SystemRoles.Ceo));
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("CEO account created for {Email}.", ceo.Email);
        }
        return 1;
    }

    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("CEO account: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
