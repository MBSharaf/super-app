using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Geo;

namespace SuperApp.Infrastructure.Persistence.Configurations.Geo;

/// <summary>Table <c>geo.Governorates</c> — see ERD.</summary>
internal sealed class GovernorateConfiguration : IEntityTypeConfiguration<Governorate>
{
    public void Configure(EntityTypeBuilder<Governorate> builder)
    {
        builder.ToTable("Governorates", "geo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(80);
        builder.Property(x => x.NameAr).HasMaxLength(80);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Governorates_Code");
        builder.HasIndex(x => x.NameEn).IsUnique().HasDatabaseName("UQ_Governorates_NameEn");
        builder.HasIndex(x => x.NameAr).IsUnique().HasDatabaseName("UQ_Governorates_NameAr");
    }
}
