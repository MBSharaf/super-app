using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Notifications;

/// <summary>Table <c>notify.NotificationPreferences</c> — see ERD.</summary>
internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences", "notify", t =>
        {
            t.HasCheckConstraint("CK_NotificationPreferences_Category", "[Category] BETWEEN 0 AND 10");
        });
        builder.HasKey(x => new { x.UserId, x.Category });
        builder.Property(x => x.Category).HasConversion<byte>();
        builder.Property(x => x.InApp).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.Email).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.Push).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
