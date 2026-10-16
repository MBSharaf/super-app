using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Shipping;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shipping;

/// <summary>Table <c>ship.Shipments</c> — see ERD.</summary>
internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments", "ship", t =>
        {
            t.HasCheckConstraint("CK_Shipments_Mode", "[Mode] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Shipments_CodAmount", "[CodAmount] >= 0");
            t.HasCheckConstraint("CK_Shipments_Status", "[Status] BETWEEN 0 AND 6");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Provider).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.Mode).HasConversion<byte>();
        builder.Property(x => x.TrackingNumber).HasMaxLength(60).IsUnicode(false);
        builder.Property(x => x.CodAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ShipmentStatus.Created).ValueGeneratedNever();
        builder.Property(x => x.IsReturn).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => new { x.Provider, x.TrackingNumber }).IsUnique().HasDatabaseName("UQ_Shipments_Provider_TrackingNumber");
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Fulfilment).WithMany().HasForeignKey(x => x.FulfilmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.LabelFile).WithMany().HasForeignKey(x => x.LabelFileId).OnDelete(DeleteBehavior.NoAction);
    }
}
