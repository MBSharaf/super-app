using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Pitches;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Bookings;

/// <summary>Table <c>booking.QrScans</c> — see ERD.</summary>
internal sealed class QrScanConfiguration : IEntityTypeConfiguration<QrScan>
{
    public void Configure(EntityTypeBuilder<QrScan> builder)
    {
        builder.ToTable("QrScans", "booking", t =>
        {
            t.HasCheckConstraint("CK_QrScans_Target", "[BookingId] IS NOT NULL OR [PackageId] IS NOT NULL");
            t.HasCheckConstraint("CK_QrScans_Result", "[Result] BETWEEN 0 AND 8");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ScannedAt).HasDefaultValueSql("SYSUTCDATETIME()").ValueGeneratedNever();
        builder.Property(x => x.Result).HasConversion<byte>();
        builder.Property(x => x.Device).HasMaxLength(120);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64).IsUnicode(false);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("UQ_QrScans_IdempotencyKey");
        builder.HasIndex(x => new { x.PackageId, x.ScannedAt }).HasDatabaseName("IX_QrScans_PackageId_ScannedAt");
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Package).WithMany().HasForeignKey(x => x.PackageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.ScannedBy).WithMany().HasForeignKey(x => x.ScannedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
    }
}
