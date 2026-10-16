using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.CatalogChangeRequests</c> — see ERD.</summary>
internal sealed class CatalogChangeRequestConfiguration : IEntityTypeConfiguration<CatalogChangeRequest>
{
    public void Configure(EntityTypeBuilder<CatalogChangeRequest> builder)
    {
        builder.ToTable("CatalogChangeRequests", "pod", t =>
        {
            t.HasCheckConstraint("CK_CatalogChangeRequests_Kind", "[Kind] BETWEEN 0 AND 4");
            t.HasCheckConstraint("CK_CatalogChangeRequests_ChangeJson_IsJson", "ISJSON([ChangeJson]) = 1");
            t.HasCheckConstraint("CK_CatalogChangeRequests_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.Reason).HasMaxLength(400);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ChangeRequestStatus.Pending).ValueGeneratedNever();
        builder.Property(x => x.ReviewNote).HasMaxLength(400);
        builder.HasIndex(x => x.Status).HasDatabaseName("IX_CatalogChangeRequests_Status");
        builder.HasOne(x => x.CatalogProduct).WithMany().HasForeignKey(x => x.CatalogProductId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RequestedByStaff).WithMany().HasForeignKey(x => x.RequestedByStaffId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.NoAction);
    }
}
