using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Fixtures</c> — see ERD.</summary>
internal sealed class FixtureConfiguration : IEntityTypeConfiguration<Fixture>
{
    public void Configure(EntityTypeBuilder<Fixture> builder)
    {
        builder.ToTable("Fixtures", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_Fixtures_Clubs", "[HomeClubId] <> [AwayClubId]");
            t.HasCheckConstraint("CK_Fixtures_Status", "[Status] BETWEEN 0 AND 4");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(FixtureStatus.Scheduled).ValueGeneratedNever();
        builder.Property(x => x.Venue).HasMaxLength(120);
        builder.HasIndex(x => x.ExternalId).IsUnique().HasDatabaseName("UQ_Fixtures_ExternalId");
        builder.HasIndex(x => x.KickoffAt).HasDatabaseName("IX_Fixtures_KickoffAt");
        builder.HasOne(x => x.Gameweek).WithMany(p => p.Fixtures).HasForeignKey(x => x.GameweekId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.HomeClub).WithMany().HasForeignKey(x => x.HomeClubId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AwayClub).WithMany().HasForeignKey(x => x.AwayClubId).OnDelete(DeleteBehavior.NoAction);
    }
}
