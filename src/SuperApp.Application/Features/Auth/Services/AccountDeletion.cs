using FluentValidation;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Domain.Enums;

namespace SuperApp.Application.Features.Auth.Services;

/// <summary>{ confirm: "DELETE", password (only for accounts that have one) }</summary>
public sealed record DeleteAccountRequest(string Confirm, string? Password);

/// <summary>Why an account can't be deleted by its owner right now (code + plain words), or null when it can.</summary>
public sealed record DeletionBlocker(string Code, string Message);

/// <summary>Data access for deleting an account (Infrastructure: EF Core + ASP.NET Identity).</summary>
public interface IAccountDeletionStore
{
    Task<DeletionBlocker?> BlockerAsync(Guid userId, CancellationToken ct);

    /// <summary>True when the account has no password (Google sign-in only) or the password is right.</summary>
    Task<bool> PasswordOkAsync(Guid userId, string? password);

    /// <summary>
    /// Personal data is wiped (name, email, phone, photo, player profile, addresses, sign-in links, phones for push) and
    /// the account is closed for good. Money records (receipts, ledger, payments) stay without the person's details, as
    /// the law on accounting records requires.
    /// </summary>
    Task AnonymizeAsync(Guid userId, CancellationToken ct);
}

/// <summary>
/// The person deletes their own account from the app or the site (Google Play account-deletion rule). Staff and business
/// accounts (pitch owners, sellers, stores, factories) and accounts with upcoming bookings or money in the wallet are
/// refused with the reason, so nobody loses money or leaves a venue without an owner; those go through support.
/// </summary>
public sealed class AccountDeletionService(IAccountDeletionStore store, ISessionService session, ICurrentUser currentUser, IAuditLogger audit)
{
    public async Task DeleteAsync(DeleteAccountRequest request, CancellationToken ct)
    {
        var me = currentUser.UserId ?? throw new UnauthorizedException();
        if (await store.BlockerAsync(me, ct) is { } blocker)
        {
            throw new ConflictException(blocker.Message, blocker.Code);
        }
        if (!await store.PasswordOkAsync(me, request.Password))
        {
            throw new BadRequestException("The password is not right.", "wrong_password");
        }
        await audit.LogAsync(AuditSystem.Site, "account.deleted", "Account deleted by its owner", "User", me.ToString(), ct: ct);
        await store.AnonymizeAsync(me, ct);
        await session.SignOutEverywhereAsync(me);
        await session.SignOutAsync();
    }
}

public sealed class DeleteAccountValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountValidator()
    {
        RuleFor(x => x.Confirm).Must(c => string.Equals(c?.Trim(), "DELETE", StringComparison.OrdinalIgnoreCase) || c?.Trim() == "حذف")
            .WithMessage("Type DELETE to confirm.");
        RuleFor(x => x.Password).MaximumLength(128);
    }
}
