using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.OpeningHours</c> — see ERD.</summary>
internal sealed class OpeningHourConfiguration : IEntityTypeConfiguration<OpeningHour>
{
    public void Configure(EntityTypeBuilder<OpeningHour> builder)
    {
        builder.ToTable("OpeningHours", "pitch", t =>
        {
            t.HasCheckConstraint("CK_OpeningHours_Hours", "[CloseHour] > [OpenHour]");
            t.HasCheckConstraint("CK_OpeningHours_DayOfWeek", "[DayOfWeek] <= 6");
            t.HasCheckConstraint("CK_OpeningHours_OpenHour", "[OpenHour] <= 23");
            t.HasCheckConstraint("CK_OpeningHours_CloseHour", "[CloseHour] BETWEEN 1 AND 30");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsClosed).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PitchId, x.DayOfWeek }).IsUnique().HasDatabaseName("UQ_OpeningHours_PitchId_DayOfWeek");
        builder.HasOne(x => x.Pitch).WithMany(p => p.OpeningHours).HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
    }
}
