using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.WaitlistEntries</c> — see ERD.</summary>
internal sealed class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("WaitlistEntries", "booking", t =>
        {
            t.HasCheckConstraint("CK_WaitlistEntries_Hour", "[Hour] <= 23");
            t.HasCheckConstraint("CK_WaitlistEntries_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(WaitlistStatus.Waiting).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PitchId, x.Date, x.Hour, x.UserId }).IsUnique().HasDatabaseName("UQ_WaitlistEntries_PitchId_Date_Hour_UserId");
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
