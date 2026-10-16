using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.PitchFacilities</c> — see ERD.</summary>
internal sealed class PitchFacilityConfiguration : IEntityTypeConfiguration<PitchFacility>
{
    public void Configure(EntityTypeBuilder<PitchFacility> builder)
    {
        builder.ToTable("PitchFacilities", "pitch");
        builder.HasKey(x => new { x.PitchId, x.FacilityId });
        builder.HasOne(x => x.Pitch).WithMany(p => p.Facilities).HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.NoAction);
    }
}
