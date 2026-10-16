using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Factories;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Factories;

/// <summary>Table <c>factory.Jobs</c> — see ERD.</summary>
internal sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs", "factory", t =>
        {
            t.HasCheckConstraint("CK_Jobs_Stage", "[Stage] BETWEEN 0 AND 6");
            t.HasCheckConstraint("CK_Jobs_DeductionAmount", "[DeductionAmount] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(16).IsUnicode(false);
        builder.Property(x => x.Stage).HasConversion<byte>().HasDefaultValue(JobStage.New).ValueGeneratedNever();
        builder.Property(x => x.ReprintCount).HasDefaultValue((byte)0).ValueGeneratedNever();
        builder.Property(x => x.IsSample).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.DeductionAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Jobs_Code");
        builder.HasIndex(x => x.OrderItemId).IsUnique().HasDatabaseName("UQ_Jobs_OrderItemId");
        builder.HasIndex(x => new { x.FactoryId, x.Stage, x.DueAt }).HasDatabaseName("IX_Jobs_FactoryId_Stage_DueAt");
        builder.HasOne(x => x.OrderItem).WithMany().HasForeignKey(x => x.OrderItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Factory).WithMany().HasForeignKey(x => x.FactoryId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AssignedStaff).WithMany().HasForeignKey(x => x.AssignedStaffId).OnDelete(DeleteBehavior.NoAction);
    }
}
