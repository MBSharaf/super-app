using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.Stores</c> — see ERD.</summary>
internal sealed class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores", "shop", t =>
        {
            t.HasCheckConstraint("CK_Stores_Type", "[Type] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Stores_Handle", "[Handle] NOT LIKE '%[^a-z0-9-]%'");
            t.HasCheckConstraint("CK_Stores_CoverPositionY", "[CoverPositionY] <= 100");
            t.HasCheckConstraint("CK_Stores_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Handle).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.Tagline).HasMaxLength(120);
        builder.Property(x => x.About).HasMaxLength(1000);
        builder.Property(x => x.CoverPositionY).HasDefaultValue((byte)50).ValueGeneratedNever();
        builder.Property(x => x.AccentColor).HasMaxLength(7).IsUnicode(false).IsFixedLength().HasDefaultValue("#17795A").ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(StoreStatus.Active).ValueGeneratedNever();
        builder.Property(x => x.BreakNote).HasMaxLength(200);
        builder.HasIndex(x => x.Handle).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Stores_Handle");
        builder.HasIndex(x => x.Type).IsUnique().HasFilter("[IsDeleted] = 0 AND [Type] = 0").HasDatabaseName("UQ_Stores_OneOfficialStore");
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.LogoFile).WithMany().HasForeignKey(x => x.LogoFileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.CoverFile).WithMany().HasForeignKey(x => x.CoverFileId).OnDelete(DeleteBehavior.NoAction);
    }
}
