using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Auth;

/// <summary>Table <c>auth.KycDocuments</c> — see ERD.</summary>
internal sealed class KycDocumentConfiguration : IEntityTypeConfiguration<KycDocument>
{
    public void Configure(EntityTypeBuilder<KycDocument> builder)
    {
        builder.ToTable("KycDocuments", "auth", t =>
        {
            t.HasCheckConstraint("CK_KycDocuments_RejectReason", "[Status] <> 2 OR [RejectReason] IS NOT NULL");
            t.HasCheckConstraint("CK_KycDocuments_Status", "[Status] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.NationalId).HasMaxLength(200).IsUnicode(false);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(KycStatus.Pending).ValueGeneratedNever();
        builder.Property(x => x.RejectReason).HasMaxLength(400);
        builder.HasIndex(x => new { x.UserId, x.Status }).HasDatabaseName("IX_KycDocuments_UserId_Status");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.FrontFile).WithMany().HasForeignKey(x => x.FrontFileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BackFile).WithMany().HasForeignKey(x => x.BackFileId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.NoAction);
    }
}
