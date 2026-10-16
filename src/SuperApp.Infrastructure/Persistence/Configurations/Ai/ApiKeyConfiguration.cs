using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ai;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ai;

/// <summary>Table <c>ai.ApiKeys</c> — see ERD.</summary>
internal sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.ToTable("ApiKeys", "ai", t =>
        {
            t.HasCheckConstraint("CK_ApiKeys_Scopes_IsJson", "[Scopes] IS NULL OR ISJSON([Scopes]) = 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.Prefix).HasMaxLength(8).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.KeyHash).HasMaxLength(64);
        builder.HasIndex(x => x.Prefix).IsUnique().HasDatabaseName("UQ_ApiKeys_Prefix");
        builder.HasOne(x => x.Agent).WithMany(p => p.ApiKeys).HasForeignKey(x => x.AgentId).OnDelete(DeleteBehavior.Cascade);
    }
}
