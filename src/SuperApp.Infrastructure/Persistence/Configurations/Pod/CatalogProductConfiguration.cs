using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.CatalogProducts</c> — see ERD.</summary>
internal sealed class CatalogProductConfiguration : IEntityTypeConfiguration<CatalogProduct>
{
    public void Configure(EntityTypeBuilder<CatalogProduct> builder)
    {
        builder.ToTable("CatalogProducts", "pod", t =>
        {
            t.HasCheckConstraint("CK_CatalogProducts_Kind", "[Kind] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_CatalogProducts_BasePrice", "[BasePrice] > 0");
            t.HasCheckConstraint("CK_CatalogProducts_FactoryCost", "[FactoryCost] > 0 AND [FactoryCost] < [BasePrice]");
            t.HasCheckConstraint("CK_CatalogProducts_BackPrintExtra", "[BackPrintExtra] >= 0");
            t.HasCheckConstraint("CK_CatalogProducts_BigSizeExtra", "[BigSizeExtra] >= 0");
            t.HasCheckConstraint("CK_CatalogProducts_MaxPrice", "[MaxPrice] > [BasePrice]");
            t.HasCheckConstraint("CK_CatalogProducts_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(80);
        builder.Property(x => x.NameAr).HasMaxLength(80);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.DescriptionEn).HasMaxLength(400);
        builder.Property(x => x.DescriptionAr).HasMaxLength(400);
        builder.Property(x => x.BasePrice).HasPrecision(12, 2);
        builder.Property(x => x.FactoryCost).HasPrecision(12, 2);
        builder.Property(x => x.BackPrintExtra).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.BigSizeExtra).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.MaxPrice).HasPrecision(12, 2);
        builder.Property(x => x.HasBackPrint).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.Model3dUrl).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(CatalogProductStatus.Active).ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_CatalogProducts_Key");
    }
}
