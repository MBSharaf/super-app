using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.PointEntries</c> — see ERD.</summary>
internal sealed class PointEntryConfiguration : IEntityTypeConfiguration<PointEntry>
{
    public void Configure(EntityTypeBuilder<PointEntry> builder)
    {
        builder.ToTable("PointEntries", "money", t =>
        {
            t.HasCheckConstraint("CK_PointEntries_Points", "[Points] <> 0");
            t.HasCheckConstraint("CK_PointEntries_Kind", "[Kind] BETWEEN 0 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.RefType).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Reason).HasMaxLength(200);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasIndex(x => new { x.UserId, x.CreatedAt }).HasDatabaseName("IX_PointEntries_UserId_CreatedAt");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
