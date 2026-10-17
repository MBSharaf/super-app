namespace SuperApp.Application.Abstractions.Identity;

/// <summary>The signed-in session (the secure cookie). Implemented with SignInManager in Infrastructure.</summary>
public interface ISessionService
{
    /// <summary>Checks the password and, when it is right, starts the session (or the pending 2FA step).</summary>
    Task<SignInOutcome> PasswordSignInAsync(Guid userId, string password, bool remember);

    Task SignOutAsync();

    /// <summary>Ends every session of the account on every device (security stamp changes).</summary>
    Task SignOutEverywhereAsync(Guid userId);
}
