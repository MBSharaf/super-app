using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.Posts</c> — see ERD.</summary>
internal sealed class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("Posts", "community", t =>
        {
            t.HasCheckConstraint("CK_Posts_Text", "LEN([Text]) >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AsSuperAppAdmin).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Text).HasMaxLength(2000);
        builder.Property(x => x.HasPoll).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.IsPinned).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.IsAnnouncement).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.CommentsClosed).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.IsHidden).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.CommentsCount).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.ReactionsCount).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CommunityId, x.CreatedAt }).HasDatabaseName("IX_Posts_CommunityId_CreatedAt");
        builder.HasIndex(x => x.AuthorId).HasDatabaseName("IX_Posts_AuthorId");
        builder.HasOne(x => x.Community).WithMany().HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ImageFile).WithMany().HasForeignKey(x => x.ImageFileId).OnDelete(DeleteBehavior.NoAction);
    }
}
