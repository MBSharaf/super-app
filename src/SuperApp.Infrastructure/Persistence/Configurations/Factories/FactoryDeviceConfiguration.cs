using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Factories;

namespace SuperApp.Infrastructure.Persistence.Configurations.Factories;

/// <summary>Table <c>factory.FactoryDevices</c> — see ERD.</summary>
internal sealed class FactoryDeviceConfiguration : IEntityTypeConfiguration<FactoryDevice>
{
    public void Configure(EntityTypeBuilder<FactoryDevice> builder)
    {
        builder.ToTable("FactoryDevices", "factory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60);
        builder.Property(x => x.TokenHash).HasMaxLength(64);
        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UQ_FactoryDevices_TokenHash");
        builder.HasOne(x => x.Factory).WithMany(p => p.Devices).HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.Cascade);
    }
}
