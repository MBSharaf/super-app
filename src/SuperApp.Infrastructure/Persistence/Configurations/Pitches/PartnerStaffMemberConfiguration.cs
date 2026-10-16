using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.PartnerStaff</c> — see ERD.</summary>
internal sealed class PartnerStaffMemberConfiguration : IEntityTypeConfiguration<PartnerStaffMember>
{
    public void Configure(EntityTypeBuilder<PartnerStaffMember> builder)
    {
        builder.ToTable("PartnerStaff", "pitch", t =>
        {
            t.HasCheckConstraint("CK_PartnerStaff_Role", "[Role] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasConversion<byte>();
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PartnerId, x.UserId }).IsUnique().HasDatabaseName("UQ_PartnerStaff_PartnerId_UserId");
        builder.HasOne(x => x.Partner).WithMany(p => p.Staff).HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
