using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.PayoutRequests</c> — see ERD.</summary>
internal sealed class PayoutRequestConfiguration : IEntityTypeConfiguration<PayoutRequest>
{
    public void Configure(EntityTypeBuilder<PayoutRequest> builder)
    {
        builder.ToTable("PayoutRequests", "money", t =>
        {
            t.HasCheckConstraint("CK_PayoutRequests_TwoPeople", "[SentById] IS NULL OR [PreparedById] IS NULL OR [SentById] <> [PreparedById]");
            t.HasCheckConstraint("CK_PayoutRequests_TransferRef", "[Status] <> 3 OR [TransferRef] IS NOT NULL");
            t.HasCheckConstraint("CK_PayoutRequests_RejectReason", "[Status] <> 4 OR [RejectReason] IS NOT NULL");
            t.HasCheckConstraint("CK_PayoutRequests_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_PayoutRequests_Method", "[Method] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_PayoutRequests_Status", "[Status] BETWEEN 0 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(14).IsUnicode(false);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Method).HasConversion<byte>();
        builder.Property(x => x.Destination).HasMaxLength(400);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(PayoutStatus.Requested).ValueGeneratedNever();
        builder.Property(x => x.TransferRef).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.RejectReason).HasMaxLength(400);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_PayoutRequests_Code");
        builder.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.PreparedBy).WithMany().HasForeignKey(x => x.PreparedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.SentBy).WithMany().HasForeignKey(x => x.SentById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Approval).WithMany().HasForeignKey(x => x.ApprovalId).OnDelete(DeleteBehavior.NoAction);
    }
}
