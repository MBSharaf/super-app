using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Support;

namespace SuperApp.Infrastructure.Persistence.Configurations.Support;

/// <summary>Table <c>support.TicketAttachments</c> — see ERD.</summary>
internal sealed class TicketAttachmentConfiguration : IEntityTypeConfiguration<TicketAttachment>
{
    public void Configure(EntityTypeBuilder<TicketAttachment> builder)
    {
        builder.ToTable("TicketAttachments", "support");
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Message).WithMany(p => p.Attachments).HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.NoAction);
    }
}
