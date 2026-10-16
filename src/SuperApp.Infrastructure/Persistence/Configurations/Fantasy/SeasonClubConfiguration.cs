using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.SeasonClubs</c> — see ERD.</summary>
internal sealed class SeasonClubConfiguration : IEntityTypeConfiguration<SeasonClub>
{
    public void Configure(EntityTypeBuilder<SeasonClub> builder)
    {
        builder.ToTable("SeasonClubs", "fantasy");
        builder.HasKey(x => new { x.SeasonId, x.ClubId });
        builder.HasOne(x => x.Season).WithMany(p => p.Clubs).HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.NoAction);
    }
}
