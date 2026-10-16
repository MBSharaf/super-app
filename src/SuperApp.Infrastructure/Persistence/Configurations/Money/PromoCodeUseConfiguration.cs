using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Bookings;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.PromoCodeUses</c> — see ERD.</summary>
internal sealed class PromoCodeUseConfiguration : IEntityTypeConfiguration<PromoCodeUse>
{
    public void Configure(EntityTypeBuilder<PromoCodeUse> builder)
    {
        builder.ToTable("PromoCodeUses", "money", t =>
        {
            t.HasCheckConstraint("CK_PromoCodeUses_Target", "[OrderId] IS NOT NULL OR [BookingId] IS NOT NULL");
            t.HasCheckConstraint("CK_PromoCodeUses_DiscountAmount", "[DiscountAmount] > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountAmount).HasPrecision(12, 2);
        builder.Property(x => x.RevenueAmount).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PromoCodeId, x.UserId }).HasDatabaseName("IX_PromoCodeUses_PromoCodeId_UserId");
        builder.HasOne(x => x.PromoCode).WithMany().HasForeignKey(x => x.PromoCodeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Booking).WithMany().HasForeignKey(x => x.BookingId).OnDelete(DeleteBehavior.NoAction);
    }
}
