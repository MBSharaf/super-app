using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.CatalogSizes</c> — see ERD.</summary>
internal sealed class CatalogSizeConfiguration : IEntityTypeConfiguration<CatalogSize>
{
    public void Configure(EntityTypeBuilder<CatalogSize> builder)
    {
        builder.ToTable("CatalogSizes", "pod", t =>
        {
            t.HasCheckConstraint("CK_CatalogSizes_ChestCm", "[ChestCm] > 0");
            t.HasCheckConstraint("CK_CatalogSizes_LengthCm", "[LengthCm] > 0");
            t.HasCheckConstraint("CK_CatalogSizes_SleeveCm", "[SleeveCm] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Size).HasMaxLength(5).IsUnicode(false);
        builder.Property(x => x.IsBigSize).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.ChestCm).HasPrecision(4, 1);
        builder.Property(x => x.LengthCm).HasPrecision(4, 1);
        builder.Property(x => x.SleeveCm).HasPrecision(4, 1);
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CatalogProductId, x.Size }).IsUnique().HasDatabaseName("UQ_CatalogSizes_CatalogProductId_Size");
        builder.HasOne(x => x.CatalogProduct).WithMany(p => p.Sizes).HasForeignKey(x => x.CatalogProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
