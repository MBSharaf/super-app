using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Communities;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.PollVotes</c> — see ERD.</summary>
internal sealed class PollVoteConfiguration : IEntityTypeConfiguration<PollVote>
{
    public void Configure(EntityTypeBuilder<PollVote> builder)
    {
        builder.ToTable("PollVotes", "community");
        builder.HasKey(x => new { x.OptionId, x.UserId });
        builder.HasIndex(x => new { x.PostId, x.UserId }).IsUnique().HasDatabaseName("UQ_PollVotes_PostId_UserId");
        builder.HasOne(x => x.Option).WithMany(p => p.Votes).HasForeignKey(x => x.OptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Post).WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.NoAction);
    }
}
