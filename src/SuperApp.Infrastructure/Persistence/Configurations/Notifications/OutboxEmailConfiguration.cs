using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Notifications;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Notifications;

/// <summary>Table <c>notify.EmailOutbox</c> — see ERD.</summary>
internal sealed class OutboxEmailConfiguration : IEntityTypeConfiguration<OutboxEmail>
{
    public void Configure(EntityTypeBuilder<OutboxEmail> builder)
    {
        builder.ToTable("EmailOutbox", "notify", t =>
        {
            t.HasCheckConstraint("CK_EmailOutbox_ModelJson_IsJson", "ISJSON([ModelJson]) = 1");
            t.HasCheckConstraint("CK_EmailOutbox_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ToAddress).HasMaxLength(256);
        builder.Property(x => x.Subject).HasMaxLength(200);
        builder.Property(x => x.Template).HasMaxLength(60).IsUnicode(false);
        builder.Property(x => x.Language).HasMaxLength(2).IsUnicode(false).IsFixedLength().HasDefaultValue("ar").ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(EmailStatus.Queued).ValueGeneratedNever();
        builder.Property(x => x.Attempts).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.Error).HasMaxLength(1000);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_EmailOutbox_Status_CreatedAt");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
