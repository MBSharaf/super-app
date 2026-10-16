using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.Series</c> — see ERD.</summary>
internal sealed class SeriesConfiguration : IEntityTypeConfiguration<Series>
{
    public void Configure(EntityTypeBuilder<Series> builder)
    {
        builder.ToTable("Series", "booking", t =>
        {
            t.HasCheckConstraint("CK_Series_DayOfWeek", "[DayOfWeek] <= 6");
            t.HasCheckConstraint("CK_Series_StartHour", "[StartHour] <= 23");
            t.HasCheckConstraint("CK_Series_Duration", "[Duration] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_Series_Status", "[Status] BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TeamName).HasMaxLength(80);
        builder.Property(x => x.ContactPhone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.AutoRenew).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(SeriesStatus.Active).ValueGeneratedNever();
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ContactUser).WithMany().HasForeignKey(x => x.ContactUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
