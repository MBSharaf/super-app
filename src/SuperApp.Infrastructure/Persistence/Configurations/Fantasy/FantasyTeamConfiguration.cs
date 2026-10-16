using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.FantasyTeams</c> — see ERD.</summary>
internal sealed class FantasyTeamConfiguration : IEntityTypeConfiguration<FantasyTeam>
{
    public void Configure(EntityTypeBuilder<FantasyTeam> builder)
    {
        builder.ToTable("FantasyTeams", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_FantasyTeams_Bank", "[Bank] >= 0");
            t.HasCheckConstraint("CK_FantasyTeams_FreeTransfers", "[FreeTransfers] <= 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(40);
        builder.Property(x => x.Bank).HasPrecision(5, 1).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.FreeTransfers).HasDefaultValue((byte)1).ValueGeneratedNever();
        builder.Property(x => x.TotalPoints).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.ChipsUsed).HasMaxLength(60).IsUnicode(false);
        builder.HasIndex(x => new { x.UserId, x.SeasonId }).IsUnique().HasDatabaseName("UQ_FantasyTeams_UserId_SeasonId");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.NoAction);
    }
}
