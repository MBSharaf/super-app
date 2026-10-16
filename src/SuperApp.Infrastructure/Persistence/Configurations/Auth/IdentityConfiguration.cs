using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Auth;

/// <summary>Table <c>auth.Users</c> (Identity) with the Super App columns — see ERD A4.</summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users", "auth", t =>
        {
            t.HasCheckConstraint("CK_Users_FullName", "LEN([FullName]) >= 2");
            t.HasCheckConstraint("CK_Users_Language", "[Language] IN ('ar', 'en')");
            t.HasCheckConstraint("CK_Users_AccountType", "[AccountType] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Users_Status", "[Status] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Users_MustPrepay", "[MustPrepay] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Users_CodRefusals", "[CodRefusals] >= 0");
            t.HasCheckConstraint("CK_Users_PhoneNumber", "[PhoneNumber] IS NULL OR [PhoneNumber] LIKE '+20%'");
        });
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.FullName).HasMaxLength(120);
        builder.Property(x => x.Language).HasMaxLength(2).IsUnicode(false).IsFixedLength().HasDefaultValue("ar").ValueGeneratedNever();
        builder.Property(x => x.AccountType).HasConversion<byte>().HasDefaultValue(Domain.Enums.AccountType.Person).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(Domain.Enums.UserStatus.Active).ValueGeneratedNever();
        builder.Property(x => x.MustPrepay).HasConversion<byte>().HasDefaultValue(Domain.Enums.PrepayRule.Automatic).ValueGeneratedNever();
        builder.Property(x => x.CodRefusals).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.SuspendedReason).HasMaxLength(400);
        builder.Property(x => x.AdminNote).HasMaxLength(1000);
        // Identity's own unique index on NormalizedEmail is replaced by one that ignores deleted accounts
        builder.HasIndex(x => x.NormalizedEmail).IsUnique().HasFilter("[IsDeleted] = 0 AND [NormalizedEmail] IS NOT NULL").HasDatabaseName("UQ_Users_NormalizedEmail");
        builder.HasIndex(x => x.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL AND [IsDeleted] = 0").HasDatabaseName("UQ_Users_PhoneNumber");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_Users_Status");
        builder.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_Users_CreatedAt");
        builder.HasOne(x => x.AvatarFile).WithMany().HasForeignKey(x => x.AvatarFileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasMany(x => x.UserRoles).WithOne(x => x.User).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Table <c>auth.Roles</c> (Identity) — see ERD A3.</summary>
internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles", "auth", t => t.HasCheckConstraint("CK_Roles_Kind", "[Kind] BETWEEN 0 AND 2"));
        builder.Property(x => x.Id).HasDefaultValueSql("NEWSEQUENTIALID()");
        builder.Property(x => x.Kind).HasConversion<byte>().HasDefaultValue(Domain.Enums.RoleKind.System).ValueGeneratedNever();
        builder.Property(x => x.DescriptionEn).HasMaxLength(400);
        builder.Property(x => x.IsLocked).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasMany(x => x.UserRoles).WithOne(x => x.Role).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Junction <c>auth.UserRoles</c> (Identity) — see ERD C1.</summary>
internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder) => builder.ToTable("UserRoles", "auth");
}

/// <summary>Identity tables B2–B5 moved into the <c>auth</c> schema.</summary>
internal sealed class IdentityChildTablesConfiguration :
    IEntityTypeConfiguration<IdentityUserClaim<Guid>>, IEntityTypeConfiguration<IdentityUserLogin<Guid>>,
    IEntityTypeConfiguration<IdentityUserToken<Guid>>, IEntityTypeConfiguration<IdentityRoleClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder) => builder.ToTable("UserClaims", "auth");
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder) => builder.ToTable("UserLogins", "auth");
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder) => builder.ToTable("UserTokens", "auth");
    public void Configure(EntityTypeBuilder<IdentityRoleClaim<Guid>> builder) => builder.ToTable("RoleClaims", "auth");
}
