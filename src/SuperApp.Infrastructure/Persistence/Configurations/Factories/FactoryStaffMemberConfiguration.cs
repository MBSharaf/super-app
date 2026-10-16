using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Factories;

/// <summary>Table <c>factory.FactoryStaff</c> — see ERD.</summary>
internal sealed class FactoryStaffMemberConfiguration : IEntityTypeConfiguration<FactoryStaffMember>
{
    public void Configure(EntityTypeBuilder<FactoryStaffMember> builder)
    {
        builder.ToTable("FactoryStaff", "factory", t =>
        {
            t.HasCheckConstraint("CK_FactoryStaff_Role", "[Role] BETWEEN 0 AND 4");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.Role).HasConversion<byte>();
        builder.Property(x => x.PinHash).HasMaxLength(64);
        builder.Property(x => x.FailedPins).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasIndex(x => new { x.FactoryId, x.Name }).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_FactoryStaff_FactoryId_Name");
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.NoAction);
    }
}
