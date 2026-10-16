using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.Departments</c> — see ERD.</summary>
internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments", "ops", t =>
        {
            t.HasCheckConstraint("CK_Departments_Color", "[Color] LIKE '#%'");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.NameEn).HasMaxLength(80);
        builder.Property(x => x.NameAr).HasMaxLength(80);
        builder.Property(x => x.Color).HasMaxLength(7).IsUnicode(false).IsFixedLength().HasDefaultValue("#0B6E4F").ValueGeneratedNever();
        builder.HasIndex(x => x.Key).IsUnique().HasDatabaseName("UQ_Departments_Key");
    }
}
