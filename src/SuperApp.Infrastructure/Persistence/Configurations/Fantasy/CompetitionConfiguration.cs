using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Competitions</c> — see ERD.</summary>
internal sealed class CompetitionConfiguration : IEntityTypeConfiguration<Competition>
{
    public void Configure(EntityTypeBuilder<Competition> builder)
    {
        builder.ToTable("Competitions", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_Competitions_SquadBudget", "[SquadBudget] > 0");
            t.HasCheckConstraint("CK_Competitions_MaxPerClub", "[MaxPerClub] BETWEEN 1 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(80);
        builder.Property(x => x.NameAr).HasMaxLength(80);
        builder.Property(x => x.Country).HasMaxLength(40);
        builder.Property(x => x.SquadBudget).HasPrecision(6, 1).HasDefaultValue(100.0m).ValueGeneratedNever();
        builder.Property(x => x.MaxPerClub).HasDefaultValue((byte)3).ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_Competitions_Key");
        builder.HasIndex(x => x.ExternalLeagueId).IsUnique().HasDatabaseName("UQ_Competitions_ExternalLeagueId");
    }
}
