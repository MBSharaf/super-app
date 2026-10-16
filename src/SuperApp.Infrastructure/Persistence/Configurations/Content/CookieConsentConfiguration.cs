using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Content;

namespace SuperApp.Infrastructure.Persistence.Configurations.Content;

/// <summary>Table <c>content.CookieConsents</c> — see ERD.</summary>
internal sealed class CookieConsentConfiguration : IEntityTypeConfiguration<CookieConsent>
{
    public void Configure(EntityTypeBuilder<CookieConsent> builder)
    {
        builder.ToTable("CookieConsents", "content");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VisitorId).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.At).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
