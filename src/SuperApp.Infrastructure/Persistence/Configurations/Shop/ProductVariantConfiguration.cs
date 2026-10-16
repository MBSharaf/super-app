using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.ProductVariants</c> — see ERD.</summary>
internal sealed class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("ProductVariants", "shop", t =>
        {
            t.HasCheckConstraint("CK_ProductVariants_Reserved", "[Reserved] >= 0 AND [Reserved] <= [Stock]");
            t.HasCheckConstraint("CK_ProductVariants_Stock", "[Stock] >= 0");
            t.HasCheckConstraint("CK_ProductVariants_PriceOverride", "[PriceOverride] IS NULL OR [PriceOverride] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Size).HasMaxLength(20).HasDefaultValue("One size").ValueGeneratedNever();
        builder.Property(x => x.ColorName).HasMaxLength(40).HasDefaultValue("Standard").ValueGeneratedNever();
        builder.Property(x => x.ColorHex).HasMaxLength(7).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.Sku).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.Stock).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.Reserved).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.PriceOverride).HasPrecision(12, 2);
        builder.HasIndex(x => x.Sku).IsUnique().HasDatabaseName("UQ_ProductVariants_Sku");
        builder.HasIndex(x => new { x.ProductId, x.Size, x.ColorName }).IsUnique().HasDatabaseName("UQ_ProductVariants_ProductId_Size_ColorName");
        builder.HasOne(x => x.Product).WithMany(p => p.Variants).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
