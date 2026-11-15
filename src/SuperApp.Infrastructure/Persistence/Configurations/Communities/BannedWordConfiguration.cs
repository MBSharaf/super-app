using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Communities;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.BannedWords</c> — see ERD.</summary>
internal sealed class BannedWordConfiguration : IEntityTypeConfiguration<BannedWord>
{
    public void Configure(EntityTypeBuilder<BannedWord> builder)
    {
        builder.ToTable("BannedWords", "community", t =>
        {
            t.HasCheckConstraint("CK_BannedWords_Language", "[Language] IN ('ar', 'en')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Word).HasMaxLength(60);
        builder.Property(x => x.Language).HasMaxLength(2).IsUnicode(false).IsFixedLength();
        builder.HasIndex(x => x.Word).IsUnique().HasDatabaseName("UQ_BannedWords_Word");
    }
}
