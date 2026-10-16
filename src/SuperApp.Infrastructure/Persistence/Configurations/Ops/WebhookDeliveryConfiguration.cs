using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.WebhookDeliveries</c> — see ERD.</summary>
internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries", "ops", t =>
        {
            t.HasCheckConstraint("CK_WebhookDeliveries_Attempt", "[Attempt] >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Error).HasMaxLength(1000);
        builder.Property(x => x.At).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasOne(x => x.Subscription).WithMany(p => p.Deliveries).HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.OutboxEvent).WithMany().HasForeignKey(x => x.OutboxEventId).OnDelete(DeleteBehavior.NoAction);
    }
}
