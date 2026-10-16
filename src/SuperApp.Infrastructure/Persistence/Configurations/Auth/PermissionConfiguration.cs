using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Persistence.Configurations.Auth;

/// <summary>Table <c>auth.Permissions</c> — see ERD.</summary>
internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", "auth", t =>
        {
            t.HasCheckConstraint("CK_Permissions_Code", "[Code] LIKE '%.%'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(80).IsUnicode(false);
        builder.Property(x => x.Area).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(120);
        builder.Property(x => x.NameAr).HasMaxLength(120);
        builder.Property(x => x.DescriptionEn).HasMaxLength(400);
        builder.Property(x => x.IsSensitive).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Permissions_Code");
        builder.HasIndex(x => x.Area).HasDatabaseName("IX_Permissions_Area");
    }
}
