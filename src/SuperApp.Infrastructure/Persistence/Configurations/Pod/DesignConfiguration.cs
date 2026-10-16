using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.Designs</c> — see ERD.</summary>
internal sealed class DesignConfiguration : IEntityTypeConfiguration<Design>
{
    public void Configure(EntityTypeBuilder<Design> builder)
    {
        builder.ToTable("Designs", "pod", t =>
        {
            t.HasCheckConstraint("CK_Designs_FrontPlacement", "([FrontX] IS NULL AND [FrontY] IS NULL AND [FrontW] IS NULL) OR ([FrontX] BETWEEN 0 AND 1 AND [FrontY] BETWEEN 0 AND 1 AND [FrontW] BETWEEN 0 AND 1)");
            t.HasCheckConstraint("CK_Designs_BackPlacement", "([BackX] IS NULL AND [BackY] IS NULL AND [BackW] IS NULL) OR ([BackX] BETWEEN 0 AND 1 AND [BackY] BETWEEN 0 AND 1 AND [BackW] BETWEEN 0 AND 1)");
            t.HasCheckConstraint("CK_Designs_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Designs_Status", "[Status] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_Designs_Views", "[Views] >= 0");
            t.HasCheckConstraint("CK_Designs_Sales", "[Sales] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(60).HasDefaultValue("").ValueGeneratedNever();
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Price).HasPrecision(12, 2);
        builder.Property(x => x.FrontX).HasPrecision(5, 4);
        builder.Property(x => x.FrontY).HasPrecision(5, 4);
        builder.Property(x => x.FrontW).HasPrecision(5, 4);
        builder.Property(x => x.BackX).HasPrecision(5, 4);
        builder.Property(x => x.BackY).HasPrecision(5, 4);
        builder.Property(x => x.BackW).HasPrecision(5, 4);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(DesignStatus.Draft).ValueGeneratedNever();
        builder.Property(x => x.RejectReason).HasMaxLength(400);
        builder.Property(x => x.SpotCheck).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Views).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.Sales).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasIndex(x => new { x.Status, x.CategoryId }).HasDatabaseName("IX_Designs_Status_CategoryId");
        builder.HasIndex(x => new { x.SellerId, x.Status }).HasDatabaseName("IX_Designs_SellerId_Status");
        builder.HasIndex(x => x.SubmittedAt).HasDatabaseName("IX_Designs_SubmittedAt");
        builder.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.CatalogProduct).WithMany().HasForeignKey(x => x.CatalogProductId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.FrontArtFile).WithMany().HasForeignKey(x => x.FrontArtFileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BackArtFile).WithMany().HasForeignKey(x => x.BackArtFileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.NoAction);
    }
}
