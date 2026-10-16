using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.Offers</c> — see ERD.</summary>
internal sealed class OfferConfiguration : IEntityTypeConfiguration<Offer>
{
    public void Configure(EntityTypeBuilder<Offer> builder)
    {
        builder.ToTable("Offers", "pitch", t =>
        {
            t.HasCheckConstraint("CK_Offers_Hours", "[ToHour] > [FromHour]");
            t.HasCheckConstraint("CK_Offers_Dates", "[EndsOn] >= [StartsOn]");
            t.HasCheckConstraint("CK_Offers_DaysMask", "[DaysMask] BETWEEN 1 AND 127");
            t.HasCheckConstraint("CK_Offers_FromHour", "[FromHour] <= 24");
            t.HasCheckConstraint("CK_Offers_ToHour", "[ToHour] <= 24");
            t.HasCheckConstraint("CK_Offers_DiscountPercent", "[DiscountPercent] > 0 AND [DiscountPercent] <= 90");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.IsActive).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasOne(x => x.Pitch).WithMany(p => p.Offers).HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
    }
}
