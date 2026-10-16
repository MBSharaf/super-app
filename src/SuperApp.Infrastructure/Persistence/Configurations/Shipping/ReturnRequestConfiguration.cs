using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shipping;

/// <summary>Table <c>ship.Returns</c> — see ERD.</summary>
internal sealed class ReturnRequestConfiguration : IEntityTypeConfiguration<ReturnRequest>
{
    public void Configure(EntityTypeBuilder<ReturnRequest> builder)
    {
        builder.ToTable("Returns", "ship", t =>
        {
            t.HasCheckConstraint("CK_Returns_Reason", "[Reason] BETWEEN 0 AND 4");
            t.HasCheckConstraint("CK_Returns_Status", "[Status] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_Returns_RefundAmount", "[RefundAmount] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(14).IsUnicode(false);
        builder.Property(x => x.Reason).HasConversion<byte>();
        builder.Property(x => x.Detail).HasMaxLength(600);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ReturnStatus.Requested).ValueGeneratedNever();
        builder.Property(x => x.RejectReason).HasMaxLength(400);
        builder.Property(x => x.RefundAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.CustomerPaysDelivery).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Returns_Code");
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.NoAction);
    }
}
