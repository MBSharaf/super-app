using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.Sellers</c> — see ERD.</summary>
internal sealed class SellerConfiguration : IEntityTypeConfiguration<Seller>
{
    public void Configure(EntityTypeBuilder<Seller> builder)
    {
        builder.ToTable("Sellers", "pod", t =>
        {
            t.HasCheckConstraint("CK_Sellers_Mode", "[Mode] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Sellers_Status", "[Status] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Sellers_LifetimeSales", "[LifetimeSales] >= 0");
            t.HasCheckConstraint("CK_Sellers_Rating", "[Rating] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_Sellers_PayoutMethod", "[PayoutMethod] BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mode).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(SellerStatus.Active).ValueGeneratedNever();
        builder.Property(x => x.LifetimeSales).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.Rating).HasPrecision(3, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.FeaturedByAdmin).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Tags).HasMaxLength(300);
        builder.Property(x => x.PayoutMethod).HasConversion<byte>();
        builder.Property(x => x.PayoutDetail).HasMaxLength(400);
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Sellers_UserId");
        builder.HasIndex(x => x.StoreId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Sellers_StoreId");
        builder.HasIndex(x => new { x.LevelId, x.Status }).HasDatabaseName("IX_Sellers_LevelId_Status");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Level).WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.FeaturedDesign).WithMany().HasForeignKey(x => x.FeaturedDesignId).OnDelete(DeleteBehavior.NoAction);
    }
}
