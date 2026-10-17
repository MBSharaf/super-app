using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Identity;
using SuperApp.Application.Common.Exceptions;
using SuperApp.Application.Common.Validation;
using SuperApp.Application.Security;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Identity;

/// <summary>Accounts, passwords and one-time tokens with ASP.NET Identity (behind <see cref="IIdentityService"/>).</summary>
public sealed class IdentityService(UserManager<User> users, AppDbContext db, PermissionReader permissions) : IIdentityService
{
    public async Task<Guid> CreateCustomerAsync(NewAccount account, string password, CancellationToken ct)
    {
        await EnsureFreeAsync(account.Email, account.Phone, null, ct);
        var user = new User
        {
            UserName = account.Email, Email = account.Email, PhoneNumber = account.Phone, FullName = account.FullName,
            Language = account.Language, AccountType = AccountType.Person, Status = UserStatus.Active,
        };
        IdentityErrors.Check(await users.CreateAsync(user, password), "password");
        IdentityErrors.Check(await users.AddToRoleAsync(user, SystemRoles.Customer));
        return user.Id;
    }

    public async Task<AccountSummary?> FindByLoginAsync(string emailOrPhone, CancellationToken ct)
    {
        User? user;
        if (emailOrPhone.Contains('@', StringComparison.Ordinal))
        {
            user = await users.FindByEmailAsync(emailOrPhone);
        }
        else
        {
            var phone = EgyptianPhone.Normalize(emailOrPhone);
            user = phone is null ? null : await db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);
        }
        return user is null ? null : await SummaryAsync(user, ct);
    }

    public async Task<AccountSummary?> FindByIdAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is null ? null : await SummaryAsync(user, ct);
    }

    public async Task<string> CreateEmailConfirmationTokenAsync(Guid userId) =>
        await users.GenerateEmailConfirmationTokenAsync(await RequireAsync(userId));

    public async Task ConfirmEmailAsync(Guid userId, string token)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw ExpiredLink();
        if (!user.EmailConfirmed && !(await users.ConfirmEmailAsync(user, token)).Succeeded)
        {
            throw ExpiredLink();
        }
    }

    public async Task<string> CreatePasswordResetTokenAsync(Guid userId) =>
        await users.GeneratePasswordResetTokenAsync(await RequireAsync(userId));

    public async Task ResetPasswordAsync(Guid userId, string token, string newPassword)
    {
        var user = await users.FindByIdAsync(userId.ToString()) ?? throw ExpiredLink();
        var result = await users.ResetPasswordAsync(user, token, newPassword);
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            throw ExpiredLink();
        }
        IdentityErrors.Check(result, "newPassword");
        await users.ResetAccessFailedCountAsync(user);
        await users.SetLockoutEndDateAsync(user, null);
    }

    public async Task ChangePasswordAsync(Guid userId, string currentPassword, string newPassword)
    {
        var user = await RequireAsync(userId);
        if (!await users.CheckPasswordAsync(user, currentPassword))
        {
            throw IdentityErrors.Field("currentPassword", "Your current password is not correct.");
        }
        IdentityErrors.Check(await users.ChangePasswordAsync(user, currentPassword, newPassword), "newPassword");
    }

    public async Task<ProfileData> GetProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await RequireAsync(userId);
        var roles = (await users.GetRolesAsync(user)).Order().ToList();
        var codes = await permissions.ForUserAsync(userId, roles, ct);
        return new ProfileData(user.Id, user.FullName, user.Email!, user.EmailConfirmed, user.PhoneNumber, user.Language,
            roles, codes, user.TwoFactorEnabled, user.CreatedAt);
    }

    public async Task UpdateProfileAsync(Guid userId, string? fullName, string? phone, string? language, CancellationToken ct)
    {
        var user = await RequireAsync(userId);
        if (phone is not null && phone != user.PhoneNumber)
        {
            await EnsureFreeAsync(null, phone, userId, ct);
            user.PhoneNumber = phone;
            user.PhoneNumberConfirmed = false;
        }
        user.FullName = fullName ?? user.FullName;
        user.Language = language ?? user.Language;
        IdentityErrors.Check(await users.UpdateAsync(user));
    }

    public async Task AddRoleAsync(Guid userId, string role)
    {
        var user = await RequireAsync(userId);
        if (!await users.IsInRoleAsync(user, role))
        {
            IdentityErrors.Check(await users.AddToRoleAsync(user, role));
        }
    }

    private async Task EnsureFreeAsync(string? email, string? phone, Guid? self, CancellationToken ct)
    {
        if (email is not null && await users.FindByEmailAsync(email) is not null)
        {
            throw new ConflictException("An account with this email already exists. Try signing in.", "email_taken");
        }
        if (phone is not null && await db.Users.IgnoreQueryFilters().AnyAsync(u => u.PhoneNumber == phone && u.Id != self, ct))
        {
            throw new ConflictException("This mobile number is already used by another account.", "phone_taken");
        }
    }

    private async Task<AccountSummary> SummaryAsync(User user, CancellationToken ct)
    {
        var isStaff = await permissions.IsStaffAsync(user.Id, ct);
        return new AccountSummary(user.Id, user.FullName, user.Email!, user.Language, user.EmailConfirmed, isStaff);
    }

    private async Task<User> RequireAsync(Guid userId) =>
        await users.FindByIdAsync(userId.ToString()) ?? throw new UnauthorizedException();

    private static BadRequestException ExpiredLink() =>
        new("This link has expired or was already used. Please ask for a new one.", "link_expired");
}
