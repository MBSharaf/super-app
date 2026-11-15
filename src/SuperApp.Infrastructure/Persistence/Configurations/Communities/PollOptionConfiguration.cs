using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Communities;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.PollOptions</c> — see ERD.</summary>
internal sealed class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
{
    public void Configure(EntityTypeBuilder<PollOption> builder)
    {
        builder.ToTable("PollOptions", "community", t =>
        {
            t.HasCheckConstraint("CK_PollOptions_SortOrder", "[SortOrder] <= 5");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Text).HasMaxLength(80);
        builder.Property(x => x.SortOrder).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.VotesCount).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasOne(x => x.Post).WithMany(p => p.PollOptions).HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
    }
}
