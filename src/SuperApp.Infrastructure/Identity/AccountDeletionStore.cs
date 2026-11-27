using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SuperApp.Application.Abstractions.Services;
using SuperApp.Application.Features.Auth.Services;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;
using SuperApp.Infrastructure.Persistence;

namespace SuperApp.Infrastructure.Identity;

public sealed class AccountDeletionStore(AppDbContext db, UserManager<User> users, PermissionReader permissions, IClock clock) : IAccountDeletionStore
{
    public async Task<DeletionBlocker?> BlockerAsync(Guid userId, CancellationToken ct)
    {
        if (await permissions.IsStaffAsync(userId, ct))
        {
            return new("staff_account", "Team accounts are closed by the CEO from the team page, not deleted here.");
        }
        if (await db.Set<Partner>().AnyAsync(p => p.UserId == userId, ct) || await db.Set<Seller>().AnyAsync(s => s.UserId == userId, ct)
            || await db.Set<Store>().AnyAsync(s => s.OwnerUserId == userId, ct) || await db.Set<Factory>().AnyAsync(f => f.OwnerUserId == userId, ct))
        {
            return new("business_account", "This account runs a business on Super Malaab. Contact support to close it, so your bookings, orders and money are settled first.");
        }
        var today = DateOnly.FromDateTime(clock.UtcNow);
        if (await db.Set<Booking>().AnyAsync(b => b.CustomerId == userId && b.Date >= today && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.AwaitingPayment), ct))
        {
            return new("open_bookings", "You have upcoming bookings. Cancel them or wait until they are played, then delete your account.");
        }
        if (await db.Set<Wallet>().AnyAsync(w => w.OwnerUserId == userId && (w.AvailableBalance > 0 || w.PendingBalance > 0), ct))
        {
            return new("wallet_balance", "Your wallet still has money. Use it or ask support to send it to you, then delete your account.");
        }
        return null;
    }

    public async Task<bool> PasswordOkAsync(Guid userId, string? password)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return false;
        }
        return !await users.HasPasswordAsync(user) || (!string.IsNullOrEmpty(password) && await users.CheckPasswordAsync(user, password));
    }

    public async Task AnonymizeAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Set<User>().FirstAsync(u => u.Id == userId, ct);
        var gone = $"deleted-{userId:N}@deleted.supermalaab.com";
        (user.FullName, user.Email, user.NormalizedEmail, user.UserName, user.NormalizedUserName) = ("Deleted user", gone, gone.ToUpperInvariant(), gone, gone.ToUpperInvariant());
        (user.PhoneNumber, user.PhoneNumberConfirmed, user.EmailConfirmed, user.PasswordHash, user.TwoFactorEnabled) = (null, false, false, null, false);
        (user.AvatarFileId, user.Position, user.Level, user.Foot, user.Area, user.AdminNote) = (null, null, null, null, null, null);
        (user.Status, user.SuspendedReason, user.LockoutEnabled, user.LockoutEnd) = (UserStatus.Suspended, "Deleted by its owner", true, DateTimeOffset.MaxValue);
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.DeletedAt = clock.UtcNow;     // kept visible (IsDeleted stays false) so old receipts and games still load, as "Deleted user"
        await db.Set<IdentityUserLogin<Guid>>().Where(l => l.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Set<IdentityUserToken<Guid>>().Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Set<PushDevice>().Where(d => d.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Set<UserAddress>().Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
        await db.SaveChangesAsync(ct);
    }
}
