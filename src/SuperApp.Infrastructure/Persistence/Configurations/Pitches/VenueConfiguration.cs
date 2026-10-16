using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.Venues</c> — see ERD.</summary>
internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("Venues", "pitch", t =>
        {
            t.HasCheckConstraint("CK_Venues_Lat", "[Lat] BETWEEN 22 AND 32");
            t.HasCheckConstraint("CK_Venues_Lng", "[Lng] BETWEEN 24 AND 37");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.Lat).HasPrecision(9, 6);
        builder.Property(x => x.Lng).HasPrecision(9, 6);
        builder.Property(x => x.Phone).HasMaxLength(20).IsUnicode(false);
        builder.HasOne(x => x.Partner).WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.NoAction);
    }
}
