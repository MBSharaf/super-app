using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.DesignTags</c> — see ERD.</summary>
internal sealed class DesignTagConfiguration : IEntityTypeConfiguration<DesignTag>
{
    public void Configure(EntityTypeBuilder<DesignTag> builder)
    {
        builder.ToTable("DesignTags", "pod");
        builder.HasKey(x => new { x.DesignId, x.Tag });
        builder.Property(x => x.Tag).HasMaxLength(40);
        builder.HasIndex(x => x.Tag).HasDatabaseName("IX_DesignTags_Tag");
        builder.HasOne(x => x.Design).WithMany(p => p.Tags).HasForeignKey(x => x.DesignId).OnDelete(DeleteBehavior.Cascade);
    }
}
