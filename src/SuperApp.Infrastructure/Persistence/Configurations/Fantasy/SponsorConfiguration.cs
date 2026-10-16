using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Sponsors</c> — see ERD.</summary>
internal sealed class SponsorConfiguration : IEntityTypeConfiguration<Sponsor>
{
    public void Configure(EntityTypeBuilder<Sponsor> builder)
    {
        builder.ToTable("Sponsors", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_Sponsors_Tier", "[Tier] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Sponsors_ContractValue", "[ContractValue] >= 0");
            t.HasCheckConstraint("CK_Sponsors_EndsOn", "[EndsOn] >= [StartsOn]");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.Tier).HasConversion<byte>();
        builder.Property(x => x.ContactEmail).HasMaxLength(256);
        builder.Property(x => x.ContractValue).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Color).HasMaxLength(7).IsUnicode(false).IsFixedLength().HasDefaultValue("#0B6E4F").ValueGeneratedNever();
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Sponsors_Name");
    }
}
