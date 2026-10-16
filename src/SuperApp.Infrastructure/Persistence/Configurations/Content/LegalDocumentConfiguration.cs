using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.LegalDocuments</c> — see ERD.</summary>
internal sealed class LegalDocumentConfiguration : IEntityTypeConfiguration<LegalDocument>
{
    public void Configure(EntityTypeBuilder<LegalDocument> builder)
    {
        builder.ToTable("LegalDocuments", "content");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.TitleEn).HasMaxLength(120);
        builder.Property(x => x.TitleAr).HasMaxLength(120);
        builder.Property(x => x.RequiresAcceptance).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_LegalDocuments_Key");
    }
}
