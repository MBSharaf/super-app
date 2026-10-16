using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.NewsPosts</c> — see ERD.</summary>
internal sealed class NewsPostConfiguration : IEntityTypeConfiguration<NewsPost>
{
    public void Configure(EntityTypeBuilder<NewsPost> builder)
    {
        builder.ToTable("NewsPosts", "content", t =>
        {
            t.HasCheckConstraint("CK_NewsPosts_Title", "LEN([Title]) >= 10");
            t.HasCheckConstraint("CK_NewsPosts_Excerpt", "LEN([Excerpt]) >= 20");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Category).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.Title).HasMaxLength(110);
        builder.Property(x => x.Excerpt).HasMaxLength(260);
        builder.Property(x => x.IsBreaking).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.IsHidden).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.PublishedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ImageFile).WithMany().HasForeignKey(x => x.ImageFileId).OnDelete(DeleteBehavior.NoAction);
    }
}
