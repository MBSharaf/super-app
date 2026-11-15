using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Bookings;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.TournamentMatches</c> — see ERD.</summary>
internal sealed class TournamentMatchConfiguration : IEntityTypeConfiguration<TournamentMatch>
{
    public void Configure(EntityTypeBuilder<TournamentMatch> builder)
    {
        builder.ToTable("TournamentMatches", "booking", t =>
        {
            t.HasCheckConstraint("CK_TournamentMatches_Teams", "[HomeTeamId] IS NULL OR [AwayTeamId] IS NULL OR [HomeTeamId] <> [AwayTeamId]");
            t.HasCheckConstraint("CK_TournamentMatches_Round", "[Round] >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Tournament).WithMany(p => p.Matches).HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.HomeTeam).WithMany().HasForeignKey(x => x.HomeTeamId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AwayTeam).WithMany().HasForeignKey(x => x.AwayTeamId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.WinnerTeam).WithMany().HasForeignKey(x => x.WinnerTeamId).OnDelete(DeleteBehavior.NoAction);
    }
}
