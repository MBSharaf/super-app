using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.Pitches</c> — see ERD.</summary>
internal sealed class PitchConfiguration : IEntityTypeConfiguration<Pitch>
{
    public void Configure(EntityTypeBuilder<Pitch> builder)
    {
        builder.ToTable("Pitches", "pitch", t =>
        {
            t.HasCheckConstraint("CK_Pitches_Format", "[Format] IN (5, 7, 11)");
            t.HasCheckConstraint("CK_Pitches_Surface", "[Surface] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Pitches_PricePerHour", "[PricePerHour] > 0");
            t.HasCheckConstraint("CK_Pitches_PeakPricePerHour", "[PeakPricePerHour] IS NULL OR [PeakPricePerHour] > 0");
            t.HasCheckConstraint("CK_Pitches_PeakFromHour", "[PeakFromHour] IS NULL OR [PeakFromHour] <= 23");
            t.HasCheckConstraint("CK_Pitches_Status", "[Status] BETWEEN 0 AND 4");
            t.HasCheckConstraint("CK_Pitches_Rating", "[Rating] BETWEEN 0 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(80).IsUnicode(false);
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.Surface).HasConversion<byte>();
        builder.Property(x => x.IsIndoor).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.PricePerHour).HasPrecision(12, 2);
        builder.Property(x => x.PeakPricePerHour).HasPrecision(12, 2);
        builder.Property(x => x.DescriptionEn).HasMaxLength(2000);
        builder.Property(x => x.DescriptionAr).HasMaxLength(2000);
        builder.Property(x => x.AllowCash).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(PitchStatus.WaitingForReview).ValueGeneratedNever();
        builder.Property(x => x.RejectReason).HasMaxLength(400);
        builder.Property(x => x.Rating).HasPrecision(3, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.ReviewsCount).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasIndex(x => x.Slug).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Pitches_Slug");
        builder.HasIndex(x => new { x.Status, x.VenueId }).HasDatabaseName("IX_Pitches_Status_VenueId");
        builder.HasIndex(x => new { x.Format, x.PricePerHour }).HasDatabaseName("IX_Pitches_Format_PricePerHour");
        builder.HasOne(x => x.Venue).WithMany().HasForeignKey(x => x.VenueId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.CancellationPolicy).WithMany().HasForeignKey(x => x.CancellationPolicyId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.NoAction);
    }
}
