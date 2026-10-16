using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.Refunds</c> — see ERD.</summary>
internal sealed class RefundConfiguration : IEntityTypeConfiguration<Refund>
{
    public void Configure(EntityTypeBuilder<Refund> builder)
    {
        builder.ToTable("Refunds", "money", t =>
        {
            t.HasCheckConstraint("CK_Refunds_Amount", "[Amount] > 0");
            t.HasCheckConstraint("CK_Refunds_Destination", "[Destination] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_Refunds_PaidBy", "[PaidBy] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_Refunds_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Destination).HasConversion<byte>();
        builder.Property(x => x.PaidBy).HasConversion<byte>();
        builder.Property(x => x.Reason).HasMaxLength(400);
        builder.Property(x => x.ProviderRef).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(RefundStatus.Requested).ValueGeneratedNever();
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Approval).WithMany().HasForeignKey(x => x.ApprovalId).OnDelete(DeleteBehavior.NoAction);
    }
}
