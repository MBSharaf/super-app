using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Communities;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.Comments</c> — see ERD.</summary>
internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments", "community");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AsSuperAppAdmin).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Text).HasMaxLength(1000);
        builder.Property(x => x.IsHidden).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasOne(x => x.Post).WithMany(p => p.Comments).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ParentComment).WithMany().HasForeignKey(x => x.ParentCommentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.NoAction);
    }
}
