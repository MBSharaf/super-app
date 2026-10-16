using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.PromoCodePitches</c> — see ERD.</summary>
internal sealed class PromoCodePitchConfiguration : IEntityTypeConfiguration<PromoCodePitch>
{
    public void Configure(EntityTypeBuilder<PromoCodePitch> builder)
    {
        builder.ToTable("PromoCodePitches", "money");
        builder.HasKey(x => new { x.PromoCodeId, x.PitchId });
        builder.HasOne(x => x.PromoCode).WithMany(p => p.Pitches).HasForeignKey(x => x.PromoCodeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Pitch).WithMany().HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.NoAction);
    }
}
