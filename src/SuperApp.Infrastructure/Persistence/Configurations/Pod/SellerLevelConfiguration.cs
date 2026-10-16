using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.SellerLevels</c> — see ERD.</summary>
internal sealed class SellerLevelConfiguration : IEntityTypeConfiguration<SellerLevel>
{
    public void Configure(EntityTypeBuilder<SellerLevel> builder)
    {
        builder.ToTable("SellerLevels", "pod", t =>
        {
            t.HasCheckConstraint("CK_SellerLevels_Rank", "[Rank] <= 9");
            t.HasCheckConstraint("CK_SellerLevels_SalesNeeded", "[SalesNeeded] >= 0");
            t.HasCheckConstraint("CK_SellerLevels_MinRating", "[MinRating] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_SellerLevels_ActiveDesigns", "[ActiveDesigns] IS NULL OR [ActiveDesigns] > 0");
            t.HasCheckConstraint("CK_SellerLevels_ActiveCodes", "[ActiveCodes] IS NULL OR [ActiveCodes] > 0");
            t.HasCheckConstraint("CK_SellerLevels_BaseDiscount", "[BaseDiscount] >= 0");
            t.HasCheckConstraint("CK_SellerLevels_StoreCommission", "[StoreCommission] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_SellerLevels_ReviewLane", "[ReviewLane] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_SellerLevels_SearchBoost", "[SearchBoost] <= 5");
            t.HasCheckConstraint("CK_SellerLevels_SampleDiscount", "[SampleDiscount] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_SellerLevels_FantasyDiscount", "[FantasyDiscount] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_SellerLevels_AnalyticsTier", "[AnalyticsTier] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_SellerLevels_CalendarTier", "[CalendarTier] <= 3");
            t.HasCheckConstraint("CK_SellerLevels_SupportTier", "[SupportTier] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(40);
        builder.Property(x => x.NameAr).HasMaxLength(40);
        builder.Property(x => x.MinRating).HasPrecision(2, 1).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.BaseDiscount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.StoreCommission).HasPrecision(5, 2);
        builder.Property(x => x.ReviewLane).HasConversion<byte>();
        builder.Property(x => x.SearchBoost).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.SampleDiscount).HasPrecision(5, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.FreeSamplesPerMonth).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.FantasyDiscount).HasPrecision(5, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.BulkUpload).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.TeamKits).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.EarlyAccess).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.AnalyticsTier).HasConversion<byte>();
        builder.Property(x => x.SupportTier).HasConversion<byte>();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_SellerLevels_Key");
        builder.HasIndex(x => x.Rank).IsUnique().HasDatabaseName("UQ_SellerLevels_Rank");
    }
}
