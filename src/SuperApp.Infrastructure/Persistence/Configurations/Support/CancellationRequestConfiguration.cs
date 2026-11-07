using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Support;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Support;

/// <summary>Table <c>support.CancellationRequests</c> — one open request per item (filtered unique index).</summary>
internal sealed class CancellationRequestConfiguration : IEntityTypeConfiguration<CancellationRequest>
{
    public void Configure(EntityTypeBuilder<CancellationRequest> builder)
    {
        builder.ToTable("CancellationRequests", "support", t =>
        {
            t.HasCheckConstraint("CK_CancellationRequests_Kind", "[Kind] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_CancellationRequests_Status", "[Status] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_CancellationRequests_Money", "[PaidOnline] >= 0 AND [RefundIfApproved] >= 0 AND ([Refunded] IS NULL OR [Refunded] >= 0)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Reference).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Label).HasMaxLength(200);
        builder.Property(x => x.PaidOnline).HasPrecision(12, 2);
        builder.Property(x => x.RefundIfApproved).HasPrecision(12, 2);
        builder.Property(x => x.Refunded).HasPrecision(12, 2);
        builder.Property(x => x.Reason).HasMaxLength(300);
        builder.Property(x => x.DecisionNote).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(CancellationStatus.Pending).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_CancellationRequests_Code");
        builder.HasIndex(x => new { x.Kind, x.Reference }).IsUnique().HasFilter("[Status] = 0").HasDatabaseName("UQ_CancellationRequests_Open");
        builder.HasIndex(x => new { x.Status, x.DueAt }).HasDatabaseName("IX_CancellationRequests_Status_DueAt");
        builder.HasIndex(x => x.CustomerId).HasDatabaseName("IX_CancellationRequests_CustomerId");
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.NoAction);
    }
}
