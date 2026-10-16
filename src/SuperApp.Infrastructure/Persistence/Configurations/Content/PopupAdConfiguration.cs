using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.PopupAds</c> — see ERD.</summary>
internal sealed class PopupAdConfiguration : IEntityTypeConfiguration<PopupAd>
{
    public void Configure(EntityTypeBuilder<PopupAd> builder)
    {
        builder.ToTable("PopupAds", "content", t =>
        {
            t.HasCheckConstraint("CK_PopupAds_Link", "[Link] LIKE '#/%'");
            t.HasCheckConstraint("CK_PopupAds_Color", "[Color] LIKE '#%'");
            t.HasCheckConstraint("CK_PopupAds_Shape", "[Shape] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_PopupAds_ShownCount", "[ShownCount] >= 0");
            t.HasCheckConstraint("CK_PopupAds_ClickedCount", "[ClickedCount] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TitleEn).HasMaxLength(60);
        builder.Property(x => x.TitleAr).HasMaxLength(60);
        builder.Property(x => x.TextEn).HasMaxLength(140);
        builder.Property(x => x.TextAr).HasMaxLength(140);
        builder.Property(x => x.CtaEn).HasMaxLength(24);
        builder.Property(x => x.CtaAr).HasMaxLength(24);
        builder.Property(x => x.Link).HasMaxLength(300);
        builder.Property(x => x.Badge).HasMaxLength(24);
        builder.Property(x => x.Color).HasMaxLength(7).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.Shape).HasConversion<byte>().HasDefaultValue(PopupShape.Cube).ValueGeneratedNever();
        builder.Property(x => x.Icons).HasMaxLength(200).IsUnicode(false);
        builder.Property(x => x.HideFromSellers).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.ShownCount).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.ClickedCount).HasDefaultValue(0).ValueGeneratedNever();
    }
}
