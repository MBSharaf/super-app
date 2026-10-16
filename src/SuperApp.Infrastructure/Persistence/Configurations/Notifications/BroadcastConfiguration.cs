using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Notifications;

/// <summary>Table <c>notify.Broadcasts</c> — see ERD.</summary>
internal sealed class BroadcastConfiguration : IEntityTypeConfiguration<Broadcast>
{
    public void Configure(EntityTypeBuilder<Broadcast> builder)
    {
        builder.ToTable("Broadcasts", "notify", t =>
        {
            t.HasCheckConstraint("CK_Broadcasts_Segment", "[Segment] BETWEEN 0 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.Body).HasMaxLength(1000);
        builder.Property(x => x.Link).HasMaxLength(300);
        builder.Property(x => x.Segment).HasConversion<byte>();
        builder.Property(x => x.SendEmail).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.RecipientsCount).HasDefaultValue(0).ValueGeneratedNever();
    }
}
