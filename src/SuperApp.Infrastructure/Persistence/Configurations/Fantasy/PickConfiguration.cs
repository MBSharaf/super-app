using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Picks</c> — see ERD.</summary>
internal sealed class PickConfiguration : IEntityTypeConfiguration<Pick>
{
    public void Configure(EntityTypeBuilder<Pick> builder)
    {
        builder.ToTable("Picks", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_Picks_Slot", "[Slot] BETWEEN 1 AND 15");
            t.HasCheckConstraint("CK_Picks_Multiplier", "[Multiplier] <= 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsCaptain).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.IsViceCaptain).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Multiplier).HasDefaultValue((byte)1).ValueGeneratedNever();
        builder.Property(x => x.Points).HasDefaultValue((short)0).ValueGeneratedNever();
        builder.HasIndex(x => new { x.TeamId, x.GameweekId, x.PlayerId }).IsUnique().HasDatabaseName("UQ_Picks_TeamId_GameweekId_PlayerId");
        builder.HasIndex(x => new { x.TeamId, x.GameweekId, x.Slot }).IsUnique().HasDatabaseName("UQ_Picks_TeamId_GameweekId_Slot");
        builder.HasOne(x => x.Team).WithMany(p => p.Picks).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Gameweek).WithMany().HasForeignKey(x => x.GameweekId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.NoAction);
    }
}
