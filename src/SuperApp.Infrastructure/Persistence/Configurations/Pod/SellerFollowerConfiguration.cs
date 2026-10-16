using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.SellerFollowers</c> — see ERD.</summary>
internal sealed class SellerFollowerConfiguration : IEntityTypeConfiguration<SellerFollower>
{
    public void Configure(EntityTypeBuilder<SellerFollower> builder)
    {
        builder.ToTable("SellerFollowers", "pod");
        builder.HasKey(x => new { x.SellerId, x.UserId });
        builder.HasOne(x => x.Seller).WithMany(p => p.Followers).HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
