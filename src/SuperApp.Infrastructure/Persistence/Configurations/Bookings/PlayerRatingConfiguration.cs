using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.PlayerRatings</c> — see ERD.</summary>
internal sealed class PlayerRatingConfiguration : IEntityTypeConfiguration<PlayerRating>
{
    public void Configure(EntityTypeBuilder<PlayerRating> builder)
    {
        builder.ToTable("PlayerRatings", "booking", t =>
        {
            t.HasCheckConstraint("CK_PlayerRatings_Stars", "[Stars] IS NULL OR [Stars] BETWEEN 1 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.GameId, x.PlayerId }).IsUnique().HasDatabaseName("UQ_PlayerRatings_GameId_PlayerId");
        builder.HasOne(x => x.Game).WithMany(p => p.Ratings).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Rater).WithMany().HasForeignKey(x => x.RaterId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.NoAction);
    }
}
