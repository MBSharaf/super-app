using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Notifications;

/// <summary>Table <c>notify.Notifications</c> — see ERD.</summary>
internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications", "notify", t =>
        {
            t.HasCheckConstraint("CK_Notifications_Category", "[Category] BETWEEN 0 AND 10");
            t.HasCheckConstraint("CK_Notifications_Link", "[Link] IS NULL OR [Link] LIKE '#/%'");
            t.HasCheckConstraint("CK_Notifications_Tone", "[Tone] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasConversion<byte>();
        builder.Property(x => x.Title).HasMaxLength(150);
        builder.Property(x => x.Body).HasMaxLength(500);
        builder.Property(x => x.Link).HasMaxLength(300);
        builder.Property(x => x.Tone).HasConversion<byte>().HasDefaultValue(NotificationTone.Info).ValueGeneratedNever();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt }).HasDatabaseName("IX_Notifications_UserId_ReadAt_CreatedAt");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
