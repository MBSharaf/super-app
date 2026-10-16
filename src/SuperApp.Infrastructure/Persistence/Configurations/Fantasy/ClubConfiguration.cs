using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Clubs</c> — see ERD.</summary>
internal sealed class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("Clubs", "fantasy");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(80);
        builder.Property(x => x.NameAr).HasMaxLength(80);
        builder.Property(x => x.ShortName).HasMaxLength(5).IsUnicode(false);
        builder.Property(x => x.KitColor).HasMaxLength(7).IsUnicode(false).IsFixedLength().HasDefaultValue("#0D1411").ValueGeneratedNever();
        builder.Property(x => x.KitColor2).HasMaxLength(7).IsUnicode(false).IsFixedLength().HasDefaultValue("#FFFFFF").ValueGeneratedNever();
        builder.Property(x => x.LogoUrl).HasMaxLength(300);
        builder.HasIndex(x => x.ExternalId).IsUnique().HasDatabaseName("UQ_Clubs_ExternalId");
    }
}
