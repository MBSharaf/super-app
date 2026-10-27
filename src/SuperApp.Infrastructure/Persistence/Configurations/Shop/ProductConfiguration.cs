using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.Products</c> — see ERD.</summary>
internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", "shop", t =>
        {
            t.HasCheckConstraint("CK_Products_CompareAtPrice", "[CompareAtPrice] IS NULL OR [CompareAtPrice] > [Price]");
            t.HasCheckConstraint("CK_Products_HiddenReason", "[HiddenByAdmin] = 0 OR [HiddenReason] IS NOT NULL");
            t.HasCheckConstraint("CK_Products_Price", "[Price] > 0");
            t.HasCheckConstraint("CK_Products_LowStockAt", "[LowStockAt] >= 0");
            t.HasCheckConstraint("CK_Products_Status", "[Status] BETWEEN 0 AND 4");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(120).IsUnicode(false);
        builder.Property(x => x.Name).HasMaxLength(150);
        builder.Property(x => x.Brand).HasMaxLength(80);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Price).HasPrecision(12, 2);
        builder.Property(x => x.CompareAtPrice).HasPrecision(12, 2);
        builder.Property(x => x.LowStockAt).HasDefaultValue(5).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ProductStatus.Draft).ValueGeneratedNever();
        builder.Property(x => x.HiddenByAdmin).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.HiddenReason).HasMaxLength(400);
        builder.Property(x => x.ReviewNote).HasMaxLength(300);
        builder.Property(x => x.Rating).HasPrecision(3, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Products_Slug");
        builder.HasIndex(x => new { x.StoreId, x.Status }).HasDatabaseName("IX_Products_StoreId_Status");
        builder.HasIndex(x => new { x.CategoryId, x.Status, x.Price }).HasDatabaseName("IX_Products_CategoryId_Status_Price");
        builder.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
    }
}
