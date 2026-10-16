using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.LedgerEntries</c> — see ERD.</summary>
internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", "money", t =>
        {
            t.HasCheckConstraint("CK_LedgerEntries_Amount", "[Amount] <> 0");
            t.HasCheckConstraint("CK_LedgerEntries_Bucket", "[Bucket] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_LedgerEntries_Kind", "[Kind] BETWEEN 0 AND 14");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(14, 2);
        builder.Property(x => x.Bucket).HasConversion<byte>();
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.RefType).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasIndex(x => new { x.WalletId, x.CreatedAt }).HasDatabaseName("IX_LedgerEntries_WalletId_CreatedAt");
        builder.HasIndex(x => new { x.Bucket, x.AvailableAt }).HasDatabaseName("IX_LedgerEntries_Bucket_AvailableAt");
        builder.HasIndex(x => new { x.RefType, x.RefId }).HasDatabaseName("IX_LedgerEntries_RefType_RefId");
        builder.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.NoAction);
    }
}
