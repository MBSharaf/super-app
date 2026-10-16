using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.OrderItems</c> — see ERD.</summary>
internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", "shop", t =>
        {
            t.HasCheckConstraint("CK_OrderItems_Kind", "([Kind] = 0 AND [ProductVariantId] IS NOT NULL AND [DesignId] IS NULL) OR ([Kind] = 1 AND [DesignId] IS NOT NULL AND [ProductVariantId] IS NULL)");
            t.HasCheckConstraint("CK_OrderItems_Kind", "[Kind] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_OrderItems_Qty", "[Qty] BETWEEN 1 AND 20");
            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "[UnitPrice] >= 0");
            t.HasCheckConstraint("CK_OrderItems_Status", "[Status] BETWEEN 0 AND 7");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.Size).HasMaxLength(40);
        builder.Property(x => x.Color).HasMaxLength(40);
        builder.Property(x => x.UnitPrice).HasPrecision(12, 2);
        builder.Property(x => x.BaseAmount).HasPrecision(12, 2);
        builder.Property(x => x.CommissionAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.SellerEarning).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(OrderItemStatus.Received).ValueGeneratedNever();
        builder.HasIndex(x => new { x.StoreId, x.Status }).HasDatabaseName("IX_OrderItems_StoreId_Status");
        builder.HasOne(x => x.Order).WithMany(p => p.Items).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Design).WithMany().HasForeignKey(x => x.DesignId).OnDelete(DeleteBehavior.NoAction);
    }
}
