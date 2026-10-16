using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.Approvals</c> — see ERD.</summary>
internal sealed class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("Approvals", "money", t =>
        {
            t.HasCheckConstraint("CK_Approvals_Kind", "[Kind] BETWEEN 0 AND 5");
            t.HasCheckConstraint("CK_Approvals_Amount", "[Amount] >= 0");
            t.HasCheckConstraint("CK_Approvals_RequiredApprovals", "[RequiredApprovals] BETWEEN 1 AND 2");
            t.HasCheckConstraint("CK_Approvals_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.RefType).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Amount).HasPrecision(14, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Summary).HasMaxLength(300);
        builder.Property(x => x.RequiredApprovals).HasDefaultValue((byte)1).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ApprovalStatus.Waiting).ValueGeneratedNever();
        builder.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_Approvals_Status_CreatedAt");
        builder.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.NoAction);
    }
}
