using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.Vouchers</c> — see ERD.</summary>
internal sealed class VoucherConfiguration : IEntityTypeConfiguration<Voucher>
{
    public void Configure(EntityTypeBuilder<Voucher> builder)
    {
        builder.ToTable("Vouchers", "money", t =>
        {
            t.HasCheckConstraint("CK_Vouchers_Amount", "[Amount] >= 100");
            t.HasCheckConstraint("CK_Vouchers_Source", "[Source] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Vouchers_PointsUsed", "[PointsUsed] >= 0");
            t.HasCheckConstraint("CK_Vouchers_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Source).HasConversion<byte>();
        builder.Property(x => x.PointsUsed).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(VoucherStatus.Active).ValueGeneratedNever();
        builder.Property(x => x.UsedOnType).HasMaxLength(30).IsUnicode(false);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Vouchers_Code");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
