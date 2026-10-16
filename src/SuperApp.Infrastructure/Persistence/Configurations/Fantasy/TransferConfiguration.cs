using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Transfers</c> — see ERD.</summary>
internal sealed class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> builder)
    {
        builder.ToTable("Transfers", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_Transfers_Players", "[PlayerOutId] <> [PlayerInId]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PriceOut).HasPrecision(4, 1);
        builder.Property(x => x.PriceIn).HasPrecision(4, 1);
        builder.Property(x => x.PointsHit).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.HasOne(x => x.Team).WithMany(p => p.Transfers).HasForeignKey(x => x.TeamId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Gameweek).WithMany().HasForeignKey(x => x.GameweekId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PlayerOut).WithMany().HasForeignKey(x => x.PlayerOutId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PlayerIn).WithMany().HasForeignKey(x => x.PlayerInId).OnDelete(DeleteBehavior.NoAction);
    }
}
