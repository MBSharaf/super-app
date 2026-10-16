using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.Budgets</c> — see ERD.</summary>
internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable("Budgets", "ops", t =>
        {
            t.HasCheckConstraint("CK_Budgets_Amount", "[Amount] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(12, 2);
        builder.HasIndex(x => new { x.DepartmentId, x.Month }).IsUnique().HasDatabaseName("UQ_Budgets_DepartmentId_Month");
        builder.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.NoAction);
    }
}
