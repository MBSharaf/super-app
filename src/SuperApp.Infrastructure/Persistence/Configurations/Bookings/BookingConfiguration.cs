using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.Bookings</c> — see ERD.</summary>
internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("Bookings", "booking", t =>
        {
            t.HasCheckConstraint("CK_Bookings_Refund", "[RefundAmount] >= 0 AND [RefundAmount] <= [Total]");
            t.HasCheckConstraint("CK_Bookings_Source", "[Source] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_Bookings_StartHour", "[StartHour] <= 23");
            t.HasCheckConstraint("CK_Bookings_Duration", "[Duration] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_Bookings_PitchAmount", "[PitchAmount] >= 0");
            t.HasCheckConstraint("CK_Bookings_DiscountAmount", "[DiscountAmount] >= 0");
            t.HasCheckConstraint("CK_Bookings_ServiceFee", "[ServiceFee] >= 0");
            t.HasCheckConstraint("CK_Bookings_Total", "[Total] >= 0");
            t.HasCheckConstraint("CK_Bookings_CommissionRate", "[CommissionRate] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Bookings_Status", "[Status] BETWEEN 0 AND 6");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(12).IsUnicode(false);
        builder.Property(x => x.WalkInName).HasMaxLength(80);
        builder.Property(x => x.WalkInPhone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Source).HasConversion<byte>();
        builder.Property(x => x.Duration).HasDefaultValue((byte)1).ValueGeneratedNever();
        builder.Property(x => x.PitchAmount).HasPrecision(12, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.ServiceFee).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Total).HasPrecision(12, 2);
        builder.Property(x => x.CommissionRate).HasPrecision(5, 2);
        builder.Property(x => x.PayAtVenue).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.PolicyKey).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(BookingStatus.Confirmed).ValueGeneratedNever();
        builder.Property(x => x.RefundAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.QrSecretHash).HasMaxLength(64);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Bookings_Code");
        builder.HasIndex(x => new { x.PitchId, x.Date }).HasDatabaseName("IX_Bookings_PitchId_Date");
        builder.HasIndex(x => new { x.CustomerId, x.Date }).HasDatabaseName("IX_Bookings_CustomerId_Date");
        builder.HasIndex(x => new { x.Status, x.Date }).HasDatabaseName("IX_Bookings_Status_Date");
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Series).WithMany().HasForeignKey(x => x.SeriesId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PromoCode).WithMany().HasForeignKey(x => x.PromoCodeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.WaiverVersion).WithMany().HasForeignKey(x => x.WaiverVersionId).OnDelete(DeleteBehavior.NoAction);
    }
}
