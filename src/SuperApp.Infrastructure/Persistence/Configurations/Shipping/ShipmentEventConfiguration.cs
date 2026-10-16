using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Shipping;

namespace SuperApp.Infrastructure.Persistence.Configurations.Shipping;

/// <summary>Table <c>ship.ShipmentEvents</c> — see ERD.</summary>
internal sealed class ShipmentEventConfiguration : IEntityTypeConfiguration<ShipmentEvent>
{
    public void Configure(EntityTypeBuilder<ShipmentEvent> builder)
    {
        builder.ToTable("ShipmentEvents", "ship", t =>
        {
            t.HasCheckConstraint("CK_ShipmentEvents_Status", "[Status] <= 6");
            t.HasCheckConstraint("CK_ShipmentEvents_RawJson_IsJson", "[RawJson] IS NULL OR ISJSON([RawJson]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.Property(x => x.ProviderEventId).HasMaxLength(80).IsUnicode(false);
        builder.HasIndex(x => x.ProviderEventId).IsUnique().HasFilter("[ProviderEventId] IS NOT NULL").HasDatabaseName("UQ_ShipmentEvents_ProviderEventId");
        builder.HasOne(x => x.Shipment).WithMany(p => p.Events).HasForeignKey(x => x.ShipmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
