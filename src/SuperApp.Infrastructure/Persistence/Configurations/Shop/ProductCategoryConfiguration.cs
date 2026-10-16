using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.ProductCategories</c> — see ERD.</summary>
internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("ProductCategories", "shop");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(60);
        builder.Property(x => x.NameAr).HasMaxLength(60);
        builder.Property(x => x.Icon).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.IsHidden).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_ProductCategories_Key");
    }
}
