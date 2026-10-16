using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.WishlistItems</c> — see ERD.</summary>
internal sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems", "shop", t =>
        {
            t.HasCheckConstraint("CK_WishlistItems_OneItem", "([ProductId] IS NULL AND [DesignId] IS NOT NULL) OR ([ProductId] IS NOT NULL AND [DesignId] IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.ProductId }).IsUnique().HasFilter("[ProductId] IS NOT NULL").HasDatabaseName("UQ_WishlistItems_Product");
        builder.HasIndex(x => new { x.UserId, x.DesignId }).IsUnique().HasFilter("[DesignId] IS NOT NULL").HasDatabaseName("UQ_WishlistItems_Design");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Design).WithMany().HasForeignKey(x => x.DesignId).OnDelete(DeleteBehavior.NoAction);
    }
}
