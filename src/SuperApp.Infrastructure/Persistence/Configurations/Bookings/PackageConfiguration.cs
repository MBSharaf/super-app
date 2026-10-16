using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.Packages</c> — see ERD.</summary>
internal sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Packages", "booking", t =>
        {
            t.HasCheckConstraint("CK_Packages_Used", "[UsedCount] <= [MatchCount]");
            t.HasCheckConstraint("CK_Packages_Dates", "[EndsOn] >= [StartsOn]");
            t.HasCheckConstraint("CK_Packages_MatchCount", "[MatchCount] BETWEEN 1 AND 12");
            t.HasCheckConstraint("CK_Packages_DayOfWeek", "[DayOfWeek] <= 6");
            t.HasCheckConstraint("CK_Packages_StartHour", "[StartHour] <= 23");
            t.HasCheckConstraint("CK_Packages_Duration", "[Duration] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_Packages_DiscountPercent", "[DiscountPercent] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Packages_TotalPrice", "[TotalPrice] > 0");
            t.HasCheckConstraint("CK_Packages_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(12).IsUnicode(false);
        builder.Property(x => x.UsedCount).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.TotalPrice).HasPrecision(12, 2);
        builder.Property(x => x.QrSecretHash).HasMaxLength(64);
        builder.Property(x => x.ExceptionUsed).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(PackageStatus.Active).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Packages_Code");
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PackageOption).WithMany().HasForeignKey(x => x.PackageOptionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ExceptionBy).WithMany().HasForeignKey(x => x.ExceptionById).OnDelete(DeleteBehavior.NoAction);
    }
}
