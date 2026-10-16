using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Factories;

/// <summary>Table <c>factory.FactoryPrices</c> — see ERD.</summary>
internal sealed class FactoryPriceConfiguration : IEntityTypeConfiguration<FactoryPrice>
{
    public void Configure(EntityTypeBuilder<FactoryPrice> builder)
    {
        builder.ToTable("FactoryPrices", "factory", t =>
        {
            t.HasCheckConstraint("CK_FactoryPrices_PricePerPiece", "[PricePerPiece] > 0");
            t.HasCheckConstraint("CK_FactoryPrices_BackPrintPrice", "[BackPrintPrice] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PricePerPiece).HasPrecision(12, 2);
        builder.Property(x => x.BackPrintPrice).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasIndex(x => new { x.FactoryId, x.CatalogProductId }).IsUnique().HasDatabaseName("UQ_FactoryPrices_FactoryId_CatalogProductId");
        builder.HasOne(x => x.Factory).WithMany(p => p.Prices).HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CatalogProduct).WithMany().HasForeignKey(x => x.CatalogProductId).OnDelete(DeleteBehavior.NoAction);
    }
}
