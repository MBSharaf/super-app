using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Geo;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Factories;

/// <summary>Table <c>factory.Factories</c> — see ERD.</summary>
internal sealed class FactoryConfiguration : IEntityTypeConfiguration<Factory>
{
    public void Configure(EntityTypeBuilder<Factory> builder)
    {
        builder.ToTable("Factories", "factory", t =>
        {
            t.HasCheckConstraint("CK_Factories_CapacityPerDay", "[CapacityPerDay] > 0");
            t.HasCheckConstraint("CK_Factories_SlaHours", "[SlaHours] BETWEEN 1 AND 240");
            t.HasCheckConstraint("CK_Factories_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(120);
        builder.Property(x => x.Address).HasMaxLength(300);
        builder.Property(x => x.SlaHours).HasDefaultValue((short)48).ValueGeneratedNever();
        builder.Property(x => x.Techniques).HasMaxLength(60).IsUnicode(false).HasDefaultValue("DTF").ValueGeneratedNever();
        builder.Property(x => x.IntakePaused).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(FactoryStatus.Active).ValueGeneratedNever();
        builder.Property(x => x.PayoutDetail).HasMaxLength(400);
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Factories_Name");
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.NoAction);
    }
}
