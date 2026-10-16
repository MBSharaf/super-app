using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.ProductSpecs</c> — see ERD.</summary>
internal sealed class ProductSpecConfiguration : IEntityTypeConfiguration<ProductSpec>
{
    public void Configure(EntityTypeBuilder<ProductSpec> builder)
    {
        builder.ToTable("ProductSpecs", "shop");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(60);
        builder.Property(x => x.Value).HasMaxLength(200);
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasOne(x => x.Product).WithMany(p => p.Specs).HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
