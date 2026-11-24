using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.FavoriteClubs</c>: up to three clubs per person (rank 1–3); the club index finds a match's followers.</summary>
internal sealed class FavoriteClubConfiguration : IEntityTypeConfiguration<FavoriteClub>
{
    public void Configure(EntityTypeBuilder<FavoriteClub> builder)
    {
        builder.ToTable("FavoriteClubs", "fantasy", t => t.HasCheckConstraint("CK_FavoriteClubs_Rank", "[Rank] BETWEEN 1 AND 3"));
        builder.HasKey(x => new { x.UserId, x.ClubId });
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasIndex(x => x.ClubId).HasDatabaseName("IX_FavoriteClubs_ClubId");
        builder.HasIndex(x => new { x.UserId, x.Rank }).IsUnique().HasDatabaseName("UX_FavoriteClubs_UserRank");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.NoAction);
    }
}
