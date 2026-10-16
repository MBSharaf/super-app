using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.Tournaments</c> — see ERD.</summary>
internal sealed class TournamentConfiguration : IEntityTypeConfiguration<Tournament>
{
    public void Configure(EntityTypeBuilder<Tournament> builder)
    {
        builder.ToTable("Tournaments", "booking", t =>
        {
            t.HasCheckConstraint("CK_Tournaments_TeamCount", "([Type] = 0 AND [TeamCount] IN (4, 8, 16)) OR ([Type] = 1 AND [TeamCount] BETWEEN 3 AND 10)");
            t.HasCheckConstraint("CK_Tournaments_Dates", "[EndsOn] >= [StartsOn]");
            t.HasCheckConstraint("CK_Tournaments_Type", "[Type] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_Tournaments_EntryFee", "[EntryFee] >= 0");
            t.HasCheckConstraint("CK_Tournaments_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.EntryFee).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.PrizeText).HasMaxLength(200);
        builder.Property(x => x.MatchHours).HasMaxLength(50).IsUnicode(false);
        builder.Property(x => x.RulesText).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(TournamentStatus.Registration).ValueGeneratedNever();
        builder.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ChampionTeam).WithMany().HasForeignKey(x => x.ChampionTeamId).OnDelete(DeleteBehavior.NoAction);
    }
}
