using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.NewsPosts</c>: stories by the team, newest first; a source link is https only.</summary>
internal sealed class NewsPostConfiguration : IEntityTypeConfiguration<NewsPost>
{
    public void Configure(EntityTypeBuilder<NewsPost> builder)
    {
        builder.ToTable("NewsPosts", "content", t =>
        {
            t.HasCheckConstraint("CK_NewsPosts_Language", "[Language] IN ('ar','en')");
            t.HasCheckConstraint("CK_NewsPosts_SourceUrl", "[SourceUrl] IS NULL OR [SourceUrl] LIKE 'https://%'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(160);
        builder.Property(x => x.Body).HasMaxLength(6000);
        builder.Property(x => x.Language).HasMaxLength(2).IsUnicode(false);
        builder.Property(x => x.SourceName).HasMaxLength(80);
        builder.Property(x => x.SourceUrl).HasMaxLength(500).IsUnicode(false);
        builder.Property(x => x.IsPublished).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasIndex(x => new { x.IsPublished, x.PublishedAt }).HasDatabaseName("IX_NewsPosts_Published");
        builder.HasIndex(x => x.ClubId).HasDatabaseName("IX_NewsPosts_ClubId");
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.SetNull);
    }
}
