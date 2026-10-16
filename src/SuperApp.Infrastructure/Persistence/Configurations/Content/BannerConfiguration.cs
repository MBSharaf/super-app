using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.Banners</c> — see ERD.</summary>
internal sealed class BannerConfiguration : IEntityTypeConfiguration<Banner>
{
    public void Configure(EntityTypeBuilder<Banner> builder)
    {
        builder.ToTable("Banners", "content", t =>
        {
            t.HasCheckConstraint("CK_Banners_Dates", "[ShowUntil] IS NULL OR [ShowFrom] IS NULL OR [ShowUntil] >= [ShowFrom]");
            t.HasCheckConstraint("CK_Banners_Title", "LEN([Title]) >= 5");
            t.HasCheckConstraint("CK_Banners_Link", "[Link] LIKE '#/%'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(70);
        builder.Property(x => x.Badge).HasMaxLength(22);
        builder.Property(x => x.CtaText).HasMaxLength(22);
        builder.Property(x => x.Link).HasMaxLength(300);
        builder.Property(x => x.ImageKey).HasMaxLength(300);
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
    }
}
