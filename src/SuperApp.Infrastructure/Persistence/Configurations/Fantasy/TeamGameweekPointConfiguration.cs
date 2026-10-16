using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.TeamGameweekPoints</c> — see ERD.</summary>
internal sealed class TeamGameweekPointConfiguration : IEntityTypeConfiguration<TeamGameweekPoint>
{
    public void Configure(EntityTypeBuilder<TeamGameweekPoint> builder)
    {
        builder.ToTable("TeamGameweekPoints", "fantasy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Points).HasDefaultValue((short)0).ValueGeneratedNever();
        builder.Property(x => x.TransfersCost).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.ChipPlayed).HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => new { x.TeamId, x.GameweekId }).IsUnique().HasDatabaseName("UQ_TeamGameweekPoints_TeamId_GameweekId");
        builder.HasIndex(x => new { x.GameweekId, x.Points }).HasDatabaseName("IX_TeamGameweekPoints_GameweekId_Points");
        builder.HasOne(x => x.Team).WithMany(p => p.GameweekPoints).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Gameweek).WithMany().HasForeignKey(x => x.GameweekId).OnDelete(DeleteBehavior.NoAction);
    }
}
