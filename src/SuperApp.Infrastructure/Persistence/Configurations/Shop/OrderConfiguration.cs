using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.Orders</c> — see ERD.</summary>
internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", "shop", t =>
        {
            t.HasCheckConstraint("CK_Orders_AddressJson_IsJson", "ISJSON([AddressJson]) = 1");
            t.HasCheckConstraint("CK_Orders_Subtotal", "[Subtotal] >= 0");
            t.HasCheckConstraint("CK_Orders_DiscountAmount", "[DiscountAmount] >= 0");
            t.HasCheckConstraint("CK_Orders_DeliveryFee", "[DeliveryFee] >= 0");
            t.HasCheckConstraint("CK_Orders_Total", "[Total] >= 0");
            t.HasCheckConstraint("CK_Orders_PaymentMethod", "[PaymentMethod] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Orders_Status", "[Status] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_Orders_DeliverySpeed", "[DeliverySpeed] BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(12).IsUnicode(false);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Subtotal).HasPrecision(12, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.DeliveryFee).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Total).HasPrecision(12, 2);
        builder.Property(x => x.VatAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.PaymentMethod).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(OrderStatus.Placed).ValueGeneratedNever();
        builder.Property(x => x.DeliverySpeed).HasConversion<byte>().HasDefaultValue(DeliverySpeed.Standard).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Orders_Code");
        builder.HasIndex(x => new { x.CustomerId, x.CreatedAt }).HasDatabaseName("IX_Orders_CustomerId_CreatedAt");
        builder.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_Orders_Status_CreatedAt");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PromoCode).WithMany().HasForeignKey(x => x.PromoCodeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
    }
}
