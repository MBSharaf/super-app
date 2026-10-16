using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.SellerStrikes</c> — see ERD.</summary>
internal sealed class SellerStrikeConfiguration : IEntityTypeConfiguration<SellerStrike>
{
    public void Configure(EntityTypeBuilder<SellerStrike> builder)
    {
        builder.ToTable("SellerStrikes", "pod");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(400);
        builder.Property(x => x.RemovedReason).HasMaxLength(400);
        builder.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Design).WithMany().HasForeignKey(x => x.DesignId).OnDelete(DeleteBehavior.SetNull);
    }
}
