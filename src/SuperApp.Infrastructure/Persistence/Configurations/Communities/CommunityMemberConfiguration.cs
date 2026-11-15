using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Communities;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Communities;

/// <summary>Table <c>community.CommunityMembers</c> — see ERD.</summary>
internal sealed class CommunityMemberConfiguration : IEntityTypeConfiguration<CommunityMember>
{
    public void Configure(EntityTypeBuilder<CommunityMember> builder)
    {
        builder.ToTable("CommunityMembers", "community", t =>
        {
            t.HasCheckConstraint("CK_CommunityMembers_Role", "[Role] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => new { x.CommunityId, x.UserId });
        builder.Property(x => x.Role).HasConversion<byte>().HasDefaultValue(CommunityRole.Member).ValueGeneratedNever();
        builder.Property(x => x.MuteReason).HasMaxLength(300);
        builder.HasOne(x => x.Community).WithMany(p => p.Members).HasForeignKey(x => x.CommunityId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
