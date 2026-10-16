using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.HelpTopics</c> — see ERD.</summary>
internal sealed class HelpTopicConfiguration : IEntityTypeConfiguration<HelpTopic>
{
    public void Configure(EntityTypeBuilder<HelpTopic> builder)
    {
        builder.ToTable("HelpTopics", "content");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(80);
        builder.Property(x => x.NameAr).HasMaxLength(80);
        builder.Property(x => x.Icon).HasMaxLength(30).IsUnicode(false);
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_HelpTopics_Key");
    }
}
