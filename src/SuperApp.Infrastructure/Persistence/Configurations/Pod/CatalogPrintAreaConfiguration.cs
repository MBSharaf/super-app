using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.CatalogPrintAreas</c> — see ERD.</summary>
internal sealed class CatalogPrintAreaConfiguration : IEntityTypeConfiguration<CatalogPrintArea>
{
    public void Configure(EntityTypeBuilder<CatalogPrintArea> builder)
    {
        builder.ToTable("CatalogPrintAreas", "pod", t =>
        {
            t.HasCheckConstraint("CK_CatalogPrintAreas_Side", "[Side] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_CatalogPrintAreas_X", "[X] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_CatalogPrintAreas_Y", "[Y] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_CatalogPrintAreas_Width", "[Width] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_CatalogPrintAreas_Height", "[Height] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_CatalogPrintAreas_PrintWidthCm", "[PrintWidthCm] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Side).HasConversion<byte>();
        builder.Property(x => x.X).HasPrecision(5, 4);
        builder.Property(x => x.Y).HasPrecision(5, 4);
        builder.Property(x => x.Width).HasPrecision(5, 4);
        builder.Property(x => x.Height).HasPrecision(5, 4);
        builder.Property(x => x.PrintWidthCm).HasPrecision(4, 1).HasDefaultValue(30m).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CatalogProductId, x.Side }).IsUnique().HasDatabaseName("UQ_CatalogPrintAreas_CatalogProductId_Side");
        builder.HasOne(x => x.CatalogProduct).WithMany(p => p.PrintAreas).HasForeignKey(x => x.CatalogProductId).OnDelete(DeleteBehavior.Cascade);
    }
}
