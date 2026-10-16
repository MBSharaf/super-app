using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.OrgPositions</c> — see ERD.</summary>
internal sealed class OrgPositionConfiguration : IEntityTypeConfiguration<OrgPosition>
{
    public void Configure(EntityTypeBuilder<OrgPosition> builder)
    {
        builder.ToTable("OrgPositions", "ops", t =>
        {
            t.HasCheckConstraint("CK_OrgPositions_DutiesJson_IsJson", "[DutiesJson] IS NULL OR ISJSON([DutiesJson]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TitleEn).HasMaxLength(120);
        builder.Property(x => x.TitleAr).HasMaxLength(120);
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReportsTo).WithMany().HasForeignKey(x => x.ReportsToId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.HolderUser).WithMany().HasForeignKey(x => x.HolderUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
