using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.SlotHolds</c> — see ERD.</summary>
internal sealed class SlotHoldConfiguration : IEntityTypeConfiguration<SlotHold>
{
    public void Configure(EntityTypeBuilder<SlotHold> builder)
    {
        builder.ToTable("SlotHolds", "booking", t =>
        {
            t.HasCheckConstraint("CK_SlotHolds_Hour", "[Hour] <= 23");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.PitchId, x.Date, x.Hour }).IsUnique().HasDatabaseName("UQ_SlotHolds_PitchId_Date_Hour");
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
