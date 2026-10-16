using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.DesignCategories</c> — see ERD.</summary>
internal sealed class DesignCategoryConfiguration : IEntityTypeConfiguration<DesignCategory>
{
    public void Configure(EntityTypeBuilder<DesignCategory> builder)
    {
        builder.ToTable("DesignCategories", "pod");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(60);
        builder.Property(x => x.NameAr).HasMaxLength(60);
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_DesignCategories_Key");
        builder.HasIndex(x => x.NameEn).IsUnique().HasDatabaseName("UQ_DesignCategories_NameEn");
    }
}
