using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.TournamentTeams</c> — see ERD.</summary>
internal sealed class TournamentTeamConfiguration : IEntityTypeConfiguration<TournamentTeam>
{
    public void Configure(EntityTypeBuilder<TournamentTeam> builder)
    {
        builder.ToTable("TournamentTeams", "booking", t =>
        {
            t.HasCheckConstraint("CK_TournamentTeams_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(TournamentTeamStatus.Registered).ValueGeneratedNever();
        builder.HasIndex(x => new { x.TournamentId, x.Name }).IsUnique().HasDatabaseName("UQ_TournamentTeams_TournamentId_Name");
        builder.HasOne(x => x.Tournament).WithMany(p => p.Teams).HasForeignKey(x => x.TournamentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Captain).WithMany().HasForeignKey(x => x.CaptainId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.WaiverVersion).WithMany().HasForeignKey(x => x.WaiverVersionId).OnDelete(DeleteBehavior.NoAction);
    }
}
