using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shop;

/// <summary>Table <c>shop.Fulfilments</c> — see ERD.</summary>
internal sealed class FulfilmentConfiguration : IEntityTypeConfiguration<Fulfilment>
{
    public void Configure(EntityTypeBuilder<Fulfilment> builder)
    {
        builder.ToTable("Fulfilments", "shop", t =>
        {
            t.HasCheckConstraint("CK_Fulfilments_Status", "[Status] BETWEEN 0 AND 4");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(FulfilmentStatus.New).ValueGeneratedNever();
        builder.HasIndex(x => new { x.OrderId, x.StoreId }).IsUnique().HasDatabaseName("UQ_Fulfilments_OrderId_StoreId");
        builder.HasOne(x => x.Order).WithMany(p => p.Fulfilments).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);
    }
}
