using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Gameweeks</c> — see ERD.</summary>
internal sealed class GameweekConfiguration : IEntityTypeConfiguration<Gameweek>
{
    public void Configure(EntityTypeBuilder<Gameweek> builder)
    {
        builder.ToTable("Gameweeks", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_Gameweeks_Number", "[Number] BETWEEN 1 AND 50");
            t.HasCheckConstraint("CK_Gameweeks_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(GameweekStatus.Upcoming).ValueGeneratedNever();
        builder.HasIndex(x => new { x.SeasonId, x.Number }).IsUnique().HasDatabaseName("UQ_Gameweeks_SeasonId_Number");
        builder.HasOne(x => x.Season).WithMany(p => p.Gameweeks).HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Cascade);
    }
}
