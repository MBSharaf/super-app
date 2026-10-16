using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ai;
using SuperApp.Domain.Entities.Auth;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ai;

/// <summary>Table <c>ai.AgentPermissions</c> — see ERD.</summary>
internal sealed class AgentPermissionConfiguration : IEntityTypeConfiguration<AgentPermission>
{
    public void Configure(EntityTypeBuilder<AgentPermission> builder)
    {
        builder.ToTable("AgentPermissions", "ai");
        builder.HasKey(x => new { x.AgentId, x.PermissionId });
        builder.HasOne(x => x.Agent).WithMany(p => p.Permissions).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionId).OnDelete(DeleteBehavior.NoAction);
    }
}
