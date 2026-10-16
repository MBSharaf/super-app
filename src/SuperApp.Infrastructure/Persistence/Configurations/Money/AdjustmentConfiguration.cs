using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.Adjustments</c> — see ERD.</summary>
internal sealed class AdjustmentConfiguration : IEntityTypeConfiguration<Adjustment>
{
    public void Configure(EntityTypeBuilder<Adjustment> builder)
    {
        builder.ToTable("Adjustments", "money", t =>
        {
            t.HasCheckConstraint("CK_Adjustments_Amount", "[Amount] <> 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.Property(x => x.Reason).HasMaxLength(400);
        builder.HasOne(x => x.Wallet).WithMany().HasForeignKey(x => x.WalletId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.By).WithMany().HasForeignKey(x => x.ById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Approval).WithMany().HasForeignKey(x => x.ApprovalId).OnDelete(DeleteBehavior.NoAction);
    }
}
