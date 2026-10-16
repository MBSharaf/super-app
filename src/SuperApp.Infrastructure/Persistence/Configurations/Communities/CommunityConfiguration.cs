using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.Communities</c> — see ERD.</summary>
internal sealed class CommunityConfiguration : IEntityTypeConfiguration<Community>
{
    public void Configure(EntityTypeBuilder<Community> builder)
    {
        builder.ToTable("Communities", "community", t =>
        {
            t.HasCheckConstraint("CK_Communities_Audience", "[Audience] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Communities_SlowModeSeconds", "[SlowModeSeconds] BETWEEN 0 AND 3600");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Slug).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.Name).HasMaxLength(80);
        builder.Property(x => x.About).HasMaxLength(500);
        builder.Property(x => x.Audience).HasConversion<byte>();
        builder.Property(x => x.RulesText).HasMaxLength(1000);
        builder.Property(x => x.SlowModeSeconds).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.Color).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Icon).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.IsArchived).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.Slug).IsUnique().HasDatabaseName("UQ_Communities_Slug");
    }
}
