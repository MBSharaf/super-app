using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Geo;

namespace SuperApp.Infrastructure.Persistence.Configurations.Auth;

/// <summary>Table <c>auth.UserAddresses</c> — see ERD.</summary>
internal sealed class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.ToTable("UserAddresses", "auth", t =>
        {
            t.HasCheckConstraint("CK_UserAddresses_Phone", "[Phone] LIKE '+20%'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(40).HasDefaultValue("Home").ValueGeneratedNever();
        builder.Property(x => x.Street).HasMaxLength(200);
        builder.Property(x => x.Building).HasMaxLength(20);
        builder.Property(x => x.Floor).HasMaxLength(20);
        builder.Property(x => x.Apartment).HasMaxLength(20);
        builder.Property(x => x.Landmark).HasMaxLength(120);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.IsDefault).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("[IsDefault] = 1").HasDatabaseName("UQ_UserAddresses_OneDefault");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.NoAction);
    }
}
