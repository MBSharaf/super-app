using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Notifications;

namespace SuperApp.Infrastructure.Persistence.Configurations.Notifications;

/// <summary>Table <c>notify.PushDevices</c>: one row per phone token; a token belongs to one person at a time.</summary>
internal sealed class PushDeviceConfiguration : IEntityTypeConfiguration<PushDevice>
{
    public void Configure(EntityTypeBuilder<PushDevice> builder)
    {
        builder.ToTable("PushDevices", "notify", t => t.HasCheckConstraint("CK_PushDevices_Platform", "[Platform] IN ('android','ios')"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).HasMaxLength(512).IsUnicode(false);
        builder.Property(x => x.Platform).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.Property(x => x.LastSeenAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasIndex(x => x.Token).IsUnique().HasDatabaseName("UX_PushDevices_Token");
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_PushDevices_UserId");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
