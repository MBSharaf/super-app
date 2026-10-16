using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.Receipts</c> — see ERD.</summary>
internal sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.ToTable("Receipts", "money", t =>
        {
            t.HasCheckConstraint("CK_Receipts_Kind", "[Kind] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_Receipts_Amount", "[Amount] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.VatAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.RefType).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.EtaUuid).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(x => x.Number).IsUnique().HasDatabaseName("UQ_Receipts_Number");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.NoAction);
    }
}
