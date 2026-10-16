using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.DesignColors</c> — see ERD.</summary>
internal sealed class DesignColorConfiguration : IEntityTypeConfiguration<DesignColor>
{
    public void Configure(EntityTypeBuilder<DesignColor> builder)
    {
        builder.ToTable("DesignColors", "pod");
        builder.HasKey(x => new { x.DesignId, x.CatalogColorId });
        builder.HasOne(x => x.Design).WithMany(p => p.Colors).HasForeignKey(x => x.DesignId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CatalogColor).WithMany().HasForeignKey(x => x.CatalogColorId).OnDelete(DeleteBehavior.NoAction);
    }
}
