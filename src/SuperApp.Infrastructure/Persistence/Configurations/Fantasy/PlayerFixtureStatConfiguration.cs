using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.PlayerFixtureStats</c> — see ERD.</summary>
internal sealed class PlayerFixtureStatConfiguration : IEntityTypeConfiguration<PlayerFixtureStat>
{
    public void Configure(EntityTypeBuilder<PlayerFixtureStat> builder)
    {
        builder.ToTable("PlayerFixtureStats", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_PlayerFixtureStats_Minutes", "[Minutes] <= 130");
            t.HasCheckConstraint("CK_PlayerFixtureStats_YellowCards", "[YellowCards] <= 2");
            t.HasCheckConstraint("CK_PlayerFixtureStats_Bonus", "[Bonus] <= 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Minutes).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.Goals).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.Assists).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.Saves).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.OwnGoals).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.PenaltiesSaved).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.PenaltiesMissed).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.YellowCards).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.RedCard).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.CleanSheet).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.GoalsConceded).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.Bonus).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.Points).HasDefaultValue((short)0).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PlayerId, x.FixtureId }).IsUnique().HasDatabaseName("UQ_PlayerFixtureStats_PlayerId_FixtureId");
        builder.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Fixture).WithMany(p => p.PlayerStats).HasForeignKey(x => x.FixtureId).OnDelete(DeleteBehavior.Cascade);
    }
}
