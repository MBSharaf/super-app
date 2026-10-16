using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.Reviews</c> — see ERD.</summary>
internal sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("Reviews", "pitch", t =>
        {
            t.HasCheckConstraint("CK_Reviews_Stars", "[Stars] BETWEEN 1 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(1000);
        builder.Property(x => x.Reply).HasMaxLength(1000);
        builder.Property(x => x.IsHidden).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.BookingId).IsUnique().HasDatabaseName("UQ_Reviews_BookingId");
        builder.HasIndex(x => new { x.PitchId, x.CreatedAt }).HasDatabaseName("IX_Reviews_PitchId_CreatedAt");
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
