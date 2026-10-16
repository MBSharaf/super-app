using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.ComplianceItems</c> — see ERD.</summary>
internal sealed class ComplianceItemConfiguration : IEntityTypeConfiguration<ComplianceItem>
{
    public void Configure(EntityTypeBuilder<ComplianceItem> builder)
    {
        builder.ToTable("ComplianceItems", "ops", t =>
        {
            t.HasCheckConstraint("CK_ComplianceItems_Repeat", "[Repeat] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200);
        builder.Property(x => x.Repeat).HasConversion<byte>();
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
