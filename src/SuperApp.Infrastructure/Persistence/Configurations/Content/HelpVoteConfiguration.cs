using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.HelpVotes</c> — see ERD.</summary>
internal sealed class HelpVoteConfiguration : IEntityTypeConfiguration<HelpVote>
{
    public void Configure(EntityTypeBuilder<HelpVote> builder)
    {
        builder.ToTable("HelpVotes", "content");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VisitorId).HasMaxLength(40).IsUnicode(false);
        builder.HasOne(x => x.Article).WithMany(p => p.Votes).HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
