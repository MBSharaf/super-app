using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Persistence.Configurations.Auth;

/// <summary>Table <c>auth.UserPermissions</c> — see ERD.</summary>
internal sealed class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("UserPermissions", "auth");
        builder.HasKey(x => new { x.UserId, x.PermissionId });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.GrantedBy).WithMany().HasForeignKey(x => x.GrantedById).OnDelete(DeleteBehavior.NoAction);
    }
}
