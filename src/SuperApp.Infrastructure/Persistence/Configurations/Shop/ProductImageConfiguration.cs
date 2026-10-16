using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.ProductImages</c> — see ERD.</summary>
internal sealed class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> builder)
    {
        builder.ToTable("ProductImages", "shop");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasOne(x => x.Product).WithMany(p => p.Images).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.NoAction);
    }
}
