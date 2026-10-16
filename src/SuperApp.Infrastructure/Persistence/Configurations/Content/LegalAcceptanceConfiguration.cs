using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.LegalAcceptances</c> — see ERD.</summary>
internal sealed class LegalAcceptanceConfiguration : IEntityTypeConfiguration<LegalAcceptance>
{
    public void Configure(EntityTypeBuilder<LegalAcceptance> builder)
    {
        builder.ToTable("LegalAcceptances", "content");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Context).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Ip).HasMaxLength(45).IsUnicode(false);
        builder.Property(x => x.AcceptedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasIndex(x => new { x.UserId, x.VersionId }).HasDatabaseName("IX_LegalAcceptances_UserId_VersionId");
        builder.HasOne(x => x.Version).WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
