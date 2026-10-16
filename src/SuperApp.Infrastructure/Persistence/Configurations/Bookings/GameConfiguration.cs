using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.Games</c> — see ERD.</summary>
internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games", "booking", t =>
        {
            t.HasCheckConstraint("CK_Games_PlayersNeeded", "[PlayersNeeded] BETWEEN 1 AND 21");
            t.HasCheckConstraint("CK_Games_Level", "[Level] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_Games_SharePrice", "[SharePrice] >= 0");
            t.HasCheckConstraint("CK_Games_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(12).IsUnicode(false);
        builder.Property(x => x.Title).HasMaxLength(100);
        builder.Property(x => x.Positions).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.Level).HasConversion<byte>();
        builder.Property(x => x.SharePrice).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.AutoAccept).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.RequestsClosed).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(GameStatus.Open).ValueGeneratedNever();
        builder.Property(x => x.HiddenByAdmin).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Games_Code");
        builder.HasIndex(x => x.BookingId).IsUnique().HasDatabaseName("UQ_Games_BookingId");
        builder.HasOne(x => x.Host).WithMany().HasForeignKey(x => x.HostId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
    }
}
