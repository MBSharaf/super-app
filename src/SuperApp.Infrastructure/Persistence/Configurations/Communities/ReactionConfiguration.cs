using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.Reactions</c> — see ERD.</summary>
internal sealed class ReactionConfiguration : IEntityTypeConfiguration<Reaction>
{
    public void Configure(EntityTypeBuilder<Reaction> builder)
    {
        builder.ToTable("Reactions", "community", t =>
        {
            t.HasCheckConstraint("CK_Reactions_OneTarget", "([PostId] IS NULL AND [CommentId] IS NOT NULL) OR ([PostId] IS NOT NULL AND [CommentId] IS NULL)");
            t.HasCheckConstraint("CK_Reactions_Type", "[Type] BETWEEN 0 AND 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.HasIndex(x => new { x.UserId, x.PostId }).IsUnique().HasFilter("[PostId] IS NOT NULL").HasDatabaseName("UQ_Reactions_Post");
        builder.HasIndex(x => new { x.UserId, x.CommentId }).IsUnique().HasFilter("[CommentId] IS NOT NULL").HasDatabaseName("UQ_Reactions_Comment");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Comment).WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.NoAction);
    }
}
