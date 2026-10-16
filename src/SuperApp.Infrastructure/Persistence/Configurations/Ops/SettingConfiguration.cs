using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.Settings</c> — see ERD.</summary>
internal sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("Settings", "ops", t =>
        {
            t.HasCheckConstraint("CK_Settings_Value_IsJson", "ISJSON([Value]) = 1");
        });
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(80).IsUnicode(false);
        builder.Property(x => x.Group).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
    }
}
