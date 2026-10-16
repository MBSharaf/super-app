using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.CartItems</c> — see ERD.</summary>
internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", "shop", t =>
        {
            t.HasCheckConstraint("CK_CartItems_OneItem", "([ProductVariantId] IS NULL AND [DesignId] IS NOT NULL) OR ([ProductVariantId] IS NOT NULL AND [DesignId] IS NULL)");
            t.HasCheckConstraint("CK_CartItems_Qty", "[Qty] BETWEEN 1 AND 20");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Size).HasMaxLength(40);
        builder.Property(x => x.Color).HasMaxLength(40);
        builder.HasIndex(x => new { x.UserId, x.ProductVariantId }).IsUnique().HasFilter("[ProductVariantId] IS NOT NULL").HasDatabaseName("UQ_CartItems_Variant");
        builder.HasIndex(x => new { x.UserId, x.DesignId, x.Size, x.Color }).IsUnique().HasFilter("[DesignId] IS NOT NULL").HasDatabaseName("UQ_CartItems_Design");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ProductVariant).WithMany().HasForeignKey(x => x.ProductVariantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Design).WithMany().HasForeignKey(x => x.DesignId).OnDelete(DeleteBehavior.NoAction);
    }
}
