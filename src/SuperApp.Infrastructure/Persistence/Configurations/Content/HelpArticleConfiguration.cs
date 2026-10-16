using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.HelpArticles</c> — see ERD.</summary>
internal sealed class HelpArticleConfiguration : IEntityTypeConfiguration<HelpArticle>
{
    public void Configure(EntityTypeBuilder<HelpArticle> builder)
    {
        builder.ToTable("HelpArticles", "content");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuestionEn).HasMaxLength(200);
        builder.Property(x => x.QuestionAr).HasMaxLength(200);
        builder.Property(x => x.AnswerEn).HasMaxLength(4000);
        builder.Property(x => x.AnswerAr).HasMaxLength(4000);
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.HelpfulCount).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.NotHelpfulCount).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.IsPublished).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasOne(x => x.Topic).WithMany().HasForeignKey(x => x.TopicId).OnDelete(DeleteBehavior.NoAction);
    }
}
