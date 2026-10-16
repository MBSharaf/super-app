using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Geo;

namespace SuperApp.Infrastructure.Persistence.Configurations.Geo;

/// <summary>Table <c>geo.Areas</c> — see ERD.</summary>
internal sealed class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("Areas", "geo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NameEn).HasMaxLength(80);
        builder.Property(x => x.NameAr).HasMaxLength(80);
        builder.Property(x => x.Lat).HasPrecision(9, 6);
        builder.Property(x => x.Lng).HasPrecision(9, 6);
        builder.HasIndex(x => new { x.GovernorateId, x.NameEn }).IsUnique().HasDatabaseName("UQ_Areas_GovernorateId_NameEn");
        builder.HasOne(x => x.Governorate).WithMany(p => p.Areas).HasForeignKey(x => x.GovernorateId).OnDelete(DeleteBehavior.NoAction);
    }
}
