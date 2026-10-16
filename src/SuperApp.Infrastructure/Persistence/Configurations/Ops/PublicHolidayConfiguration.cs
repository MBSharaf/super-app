using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.PublicHolidays</c> — see ERD.</summary>
internal sealed class PublicHolidayConfiguration : IEntityTypeConfiguration<PublicHoliday>
{
    public void Configure(EntityTypeBuilder<PublicHoliday> builder)
    {
        builder.ToTable("PublicHolidays", "ops", t =>
        {
            t.HasCheckConstraint("CK_PublicHolidays_Kind", "[Kind] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_PublicHolidays_Source", "[Source] IN ('nagerdate', 'calendarific', 'admin')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Country).HasMaxLength(2).IsUnicode(false).IsFixedLength().HasDefaultValue("EG").ValueGeneratedNever();
        builder.Property(x => x.NameEn).HasMaxLength(120);
        builder.Property(x => x.NameAr).HasMaxLength(120);
        builder.Property(x => x.Kind).HasConversion<byte>();
        builder.Property(x => x.IsDayOff).HasDefaultValue(true).ValueGeneratedNever();
        builder.Property(x => x.IsEstimated).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Source).HasMaxLength(20).IsUnicode(false);
        builder.HasIndex(x => new { x.Country, x.Date, x.NameEn }).IsUnique().HasDatabaseName("UQ_PublicHolidays_Country_Date_NameEn");
        builder.HasIndex(x => x.Date).HasDatabaseName("IX_PublicHolidays_Date");
    }
}
