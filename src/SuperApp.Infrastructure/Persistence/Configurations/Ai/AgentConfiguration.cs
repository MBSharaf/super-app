using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ai;

/// <summary>Table <c>ai.Agents</c> — see ERD.</summary>
internal sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents", "ai", t =>
        {
            t.HasCheckConstraint("CK_Agents_Platform", "[Platform] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Agents_MoneyLimit", "[MoneyLimit] >= 0");
            t.HasCheckConstraint("CK_Agents_MonthlyBudget", "[MonthlyBudget] >= 0");
            t.HasCheckConstraint("CK_Agents_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60);
        builder.Property(x => x.TitleEn).HasMaxLength(120);
        builder.Property(x => x.TitleAr).HasMaxLength(120);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Model).HasMaxLength(60).IsUnicode(false);
        builder.Property(x => x.Platform).HasConversion<byte>().HasDefaultValue(AgentPlatform.N8n).ValueGeneratedNever();
        builder.Property(x => x.MoneyLimit).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.MonthlyBudget).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(AgentStatus.Paused).ValueGeneratedNever();
        builder.Property(x => x.PausedReason).HasMaxLength(400);
        builder.HasIndex(x => x.ServiceUserId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Agents_ServiceUserId");
        builder.HasIndex(x => x.PositionId).IsUnique().HasFilter("[IsDeleted] = 0 AND [PositionId] IS NOT NULL").HasDatabaseName("UQ_Agents_PositionId");
        builder.HasIndex(x => x.Name).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Agents_Name");
        builder.HasOne(x => x.ServiceUser).WithMany().HasForeignKey(x => x.ServiceUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Position).WithMany().HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.SetNull);
    }
}
