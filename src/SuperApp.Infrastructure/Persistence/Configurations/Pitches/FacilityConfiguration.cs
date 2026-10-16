using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.Facilities</c> — see ERD.</summary>
internal sealed class FacilityConfiguration : IEntityTypeConfiguration<Facility>
{
    public void Configure(EntityTypeBuilder<Facility> builder)
    {
        builder.ToTable("Facilities", "pitch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(60);
        builder.Property(x => x.NameAr).HasMaxLength(60);
        builder.Property(x => x.Icon).HasMaxLength(30).IsUnicode(false);
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_Facilities_Key");
    }
}
