using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.CatalogDetails</c> — see ERD.</summary>
internal sealed class CatalogDetailConfiguration : IEntityTypeConfiguration<CatalogDetail>
{
    public void Configure(EntityTypeBuilder<CatalogDetail> builder)
    {
        builder.ToTable("CatalogDetails", "pod");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.LabelEn).HasMaxLength(40);
        builder.Property(x => x.LabelAr).HasMaxLength(40);
        builder.Property(x => x.ValueEn).HasMaxLength(300);
        builder.Property(x => x.ValueAr).HasMaxLength(300);
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasOne(x => x.CatalogProduct).WithMany(p => p.Details).HasForeignKey(x => x.CatalogProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
