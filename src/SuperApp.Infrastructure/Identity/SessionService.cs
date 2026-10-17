using Microsoft.AspNetCore.Identity;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Identity;

/// <summary>
/// The sign-in cookie, with SignInManager. The password is checked first (so a wrong password never reveals
/// whether an account is suspended); 5 wrong tries lock the account for 15 minutes.
/// </summary>
public sealed class SessionService(SignInManager<User> signIn, UserManager<User> users) : ISessionService
{
    public async Task<SignInOutcome> PasswordSignInAsync(Guid userId, string password, bool remember)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return SignInOutcome.Failed;
        }
        var check = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return SignInOutcome.LockedOut;
        }
        if (!check.Succeeded)
        {
            return SignInOutcome.Failed;
        }
        if (user.Status == UserStatus.Suspended || user.IsDeleted)
        {
            return SignInOutcome.NotAllowed;
        }
        // Starts the session, or the pending two-factor step when the account has an authenticator
        var result = await signIn.PasswordSignInAsync(user, password, remember, lockoutOnFailure: false);
        return result.RequiresTwoFactor ? SignInOutcome.RequiresTwoFactor : result.Succeeded ? SignInOutcome.Succeeded : SignInOutcome.Failed;
    }

    public Task SignOutAsync() => signIn.SignOutAsync();

    public async Task SignOutEverywhereAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is not null)
        {
            await users.UpdateSecurityStampAsync(user);
        }
    }
}
