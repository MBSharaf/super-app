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
            t.HasCheckConstraint("CK_Competitions_Provider", "[Provider] IN ('fpl', 'thesportsdb', 'api-football')");
            t.HasCheckConstraint("CK_Competitions_Color", "[Color] LIKE '#%'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(80);
        builder.Property(x => x.NameAr).HasMaxLength(80);
        builder.Property(x => x.Country).HasMaxLength(40);
        builder.Property(x => x.SquadBudget).HasPrecision(6, 1).HasDefaultValue(100.0m).ValueGeneratedNever();
        builder.Property(x => x.MaxPerClub).HasDefaultValue((byte)3).ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_Competitions_Key");
        builder.Property(x => x.Provider).HasMaxLength(20).IsUnicode(false).HasDefaultValue("api-football").ValueGeneratedNever();
        builder.Property(x => x.FantasyEnabled).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.ShortName).HasMaxLength(30).HasDefaultValue(string.Empty).ValueGeneratedNever();
        builder.Property(x => x.Region).HasMaxLength(30).HasDefaultValue(string.Empty).ValueGeneratedNever();
        builder.Property(x => x.Color).HasMaxLength(9).IsUnicode(false).HasDefaultValue("#0B6E4F").ValueGeneratedNever();
        builder.HasIndex(x => new { x.Provider, x.ExternalLeagueId }).IsUnique().HasDatabaseName("UQ_Competitions_Provider_ExternalLeagueId");
    }
}
