using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Persistence.Configurations.Auth;

/// <summary>Table <c>auth.StaffLimits</c> — see ERD.</summary>
internal sealed class StaffLimitConfiguration : IEntityTypeConfiguration<StaffLimit>
{
    public void Configure(EntityTypeBuilder<StaffLimit> builder)
    {
        builder.ToTable("StaffLimits", "auth", t =>
        {
            t.HasCheckConstraint("CK_StaffLimits_RefundLimit", "[RefundLimit] >= 0");
            t.HasCheckConstraint("CK_StaffLimits_CreditLimit", "[CreditLimit] >= 0");
            t.HasCheckConstraint("CK_StaffLimits_PayoutLimit", "[PayoutLimit] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RefundLimit).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.CreditLimit).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.PayoutLimit).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.CanSecondApprove).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("UQ_StaffLimits_UserId");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
