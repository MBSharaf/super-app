using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.SlotAlerts</c> — see ERD.</summary>
internal sealed class SlotAlertConfiguration : IEntityTypeConfiguration<SlotAlert>
{
    public void Configure(EntityTypeBuilder<SlotAlert> builder)
    {
        builder.ToTable("SlotAlerts", "booking", t =>
        {
            t.HasCheckConstraint("CK_SlotAlerts_Target", "[PitchId] IS NOT NULL OR [AreaId] IS NOT NULL");
            t.HasCheckConstraint("CK_SlotAlerts_FromHour", "[FromHour] IS NULL OR [FromHour] <= 24");
            t.HasCheckConstraint("CK_SlotAlerts_ToHour", "[ToHour] IS NULL OR [ToHour] <= 24");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.NoAction);
    }
}
