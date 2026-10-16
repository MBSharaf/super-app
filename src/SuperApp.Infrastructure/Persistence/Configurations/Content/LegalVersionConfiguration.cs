using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.LegalVersions</c> — see ERD.</summary>
internal sealed class LegalVersionConfiguration : IEntityTypeConfiguration<LegalVersion>
{
    public void Configure(EntityTypeBuilder<LegalVersion> builder)
    {
        builder.ToTable("LegalVersions", "content", t =>
        {
            t.HasCheckConstraint("CK_LegalVersions_Version", "[Version] >= 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SummaryEn).HasMaxLength(400);
        builder.Property(x => x.SummaryAr).HasMaxLength(400);
        builder.Property(x => x.LawyerNote).HasMaxLength(400);
        builder.HasIndex(x => new { x.DocumentId, x.Version }).IsUnique().HasDatabaseName("UQ_LegalVersions_DocumentId_Version");
        builder.HasOne(x => x.Document).WithMany(p => p.Versions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.NoAction);
    }
}
