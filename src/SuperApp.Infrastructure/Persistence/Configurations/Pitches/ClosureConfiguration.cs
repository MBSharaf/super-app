using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.Closures</c> — see ERD.</summary>
internal sealed class ClosureConfiguration : IEntityTypeConfiguration<Closure>
{
    public void Configure(EntityTypeBuilder<Closure> builder)
    {
        builder.ToTable("Closures", "pitch", t =>
        {
            t.HasCheckConstraint("CK_Closures_Dates", "[ToDate] >= [FromDate]");
            t.HasCheckConstraint("CK_Closures_Hours", "([FromHour] IS NULL AND [ToHour] IS NULL) OR ([FromHour] <= 24 AND [ToHour] <= 24 AND [ToHour] > [FromHour])");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(200);
        builder.HasIndex(x => new { x.PitchId, x.FromDate }).HasDatabaseName("IX_Closures_PitchId_FromDate");
        builder.HasOne(x => x.Pitch).WithMany(p => p.Closures).HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
    }
}
