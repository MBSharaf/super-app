using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.Partners</c> — see ERD.</summary>
internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> builder)
    {
        builder.ToTable("Partners", "pitch", t =>
        {
            t.HasCheckConstraint("CK_Partners_BookingCommission", "[BookingCommission] IS NULL OR [BookingCommission] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Partners_StoreCommission", "[StoreCommission] IS NULL OR [StoreCommission] BETWEEN 0 AND 100");
            t.HasCheckConstraint("CK_Partners_PayoutMethod", "[PayoutMethod] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_Partners_Status", "[Status] BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.BusinessName).HasMaxLength(120);
        builder.Property(x => x.BookingCommission).HasPrecision(5, 2);
        builder.Property(x => x.StoreCommission).HasPrecision(5, 2);
        builder.Property(x => x.PayoutMethod).HasConversion<byte>();
        builder.Property(x => x.PayoutDetail).HasMaxLength(400);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(PartnerStatus.Active).ValueGeneratedNever();
        builder.Property(x => x.SuspendedReason).HasMaxLength(400);
        builder.Property(x => x.SlotPostingBlocked).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => x.UserId).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Partners_UserId");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
