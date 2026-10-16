using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.CatalogColors</c> — see ERD.</summary>
internal sealed class CatalogColorConfiguration : IEntityTypeConfiguration<CatalogColor>
{
    public void Configure(EntityTypeBuilder<CatalogColor> builder)
    {
        builder.ToTable("CatalogColors", "pod", t =>
        {
            t.HasCheckConstraint("CK_CatalogColors_Hex", "[Hex] LIKE '#%'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(40);
        builder.Property(x => x.NameAr).HasMaxLength(40);
        builder.Property(x => x.Hex).HasMaxLength(7).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CatalogProductId, x.NameEn }).IsUnique().HasDatabaseName("UQ_CatalogColors_CatalogProductId_NameEn");
        builder.HasOne(x => x.CatalogProduct).WithMany(p => p.Colors).HasForeignKey(x => x.CatalogProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
