using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.WebhookSubscriptions</c> — see ERD.</summary>
internal sealed class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("WebhookSubscriptions", "ops", t =>
        {
            t.HasCheckConstraint("CK_WebhookSubscriptions_Url", "[Url] LIKE 'https://%' OR [Url] LIKE 'http://localhost%'");
            t.HasCheckConstraint("CK_WebhookSubscriptions_Events_IsJson", "ISJSON([Events]) = 1");
            t.HasCheckConstraint("CK_WebhookSubscriptions_FailureCount", "[FailureCount] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.Url).HasMaxLength(500);
        builder.Property(x => x.SecretHash).HasMaxLength(64);
        builder.Property(x => x.IsEnabled).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.FailureCount).HasDefaultValue(0).ValueGeneratedNever();
    }
}
