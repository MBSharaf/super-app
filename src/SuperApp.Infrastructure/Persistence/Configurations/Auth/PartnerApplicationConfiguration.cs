using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Auth;

/// <summary>Table <c>auth.Applications</c> — see ERD.</summary>
internal sealed class PartnerApplicationConfiguration : IEntityTypeConfiguration<PartnerApplication>
{
    public void Configure(EntityTypeBuilder<PartnerApplication> builder)
    {
        builder.ToTable("Applications", "auth", t =>
        {
            t.HasCheckConstraint("CK_Applications_RejectReason", "[Status] <> 2 OR [RejectReason] IS NOT NULL");
            t.HasCheckConstraint("CK_Applications_Type", "[Type] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_Applications_AnswersJson_IsJson", "ISJSON([AnswersJson]) = 1");
            t.HasCheckConstraint("CK_Applications_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(12).IsUnicode(false);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(ApplicationStatus.Pending).ValueGeneratedNever();
        builder.Property(x => x.RejectReason).HasMaxLength(400);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UQ_Applications_Code");
        builder.HasIndex(x => new { x.Status, x.Type, x.CreatedAt }).HasDatabaseName("IX_Applications_Status_Type_CreatedAt");
        builder.HasIndex(x => new { x.ApplicantId, x.Type }).IsUnique().HasFilter("[Status] = 0").HasDatabaseName("UQ_Applications_OneOpenPerType");
        builder.HasOne(x => x.Applicant).WithMany().HasForeignKey(x => x.ApplicantId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AgreementVersion).WithMany().HasForeignKey(x => x.AgreementVersionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.NoAction);
    }
}
