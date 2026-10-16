using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shipping;

/// <summary>Table <c>ship.ReturnItems</c> — see ERD.</summary>
internal sealed class ReturnItemConfiguration : IEntityTypeConfiguration<ReturnItem>
{
    public void Configure(EntityTypeBuilder<ReturnItem> builder)
    {
        builder.ToTable("ReturnItems", "ship", t =>
        {
            t.HasCheckConstraint("CK_ReturnItems_Qty", "[Qty] >= 1");
            t.HasCheckConstraint("CK_ReturnItems_Action", "[Action] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<byte>();
        builder.Property(x => x.NewSize).HasMaxLength(40);
        builder.Property(x => x.NewColor).HasMaxLength(40);
        builder.HasOne(x => x.Return).WithMany(p => p.Items).HasForeignKey(x => x.ReturnId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.OrderItem).WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.NoAction);
    }
}
