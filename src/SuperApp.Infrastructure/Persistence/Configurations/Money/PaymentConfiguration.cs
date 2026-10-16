using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.Payments</c> — see ERD.</summary>
internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", "money", t =>
        {
            t.HasCheckConstraint("CK_Payments_Vat", "[VatAmount] >= 0 AND [VatAmount] <= [Amount]");
            t.HasCheckConstraint("CK_Payments_Purpose", "[Purpose] BETWEEN 0 AND 7");
            t.HasCheckConstraint("CK_Payments_Method", "[Method] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_Payments_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Payments_Status", "[Status] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_Payments_RawWebhookJson_IsJson", "[RawWebhookJson] IS NULL OR ISJSON([RawWebhookJson]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(16).IsUnicode(false);
        builder.Property(x => x.Provider).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.ProviderRef).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64).IsUnicode(false);
        builder.Property(x => x.Purpose).HasConversion<byte>();
        builder.Property(x => x.Method).HasConversion<byte>();
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.VatAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength().HasDefaultValue("EGP").ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(PaymentStatus.Created).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Payments_Code");
        builder.HasIndex(x => x.ProviderRef).IsUnique().HasFilter("[ProviderRef] IS NOT NULL").HasDatabaseName("UQ_Payments_ProviderRef");
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UQ_Payments_IdempotencyKey");
        builder.HasIndex(x => new { x.PayerUserId, x.CreatedAt }).HasDatabaseName("IX_Payments_PayerUserId_CreatedAt");
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_Payments_Status");
        builder.HasOne(x => x.PayerUser).WithMany().HasForeignKey(x => x.PayerUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
