using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.FantasyGroups</c> — see ERD.</summary>
internal sealed class FantasyGroupConfiguration : IEntityTypeConfiguration<FantasyGroup>
{
    public void Configure(EntityTypeBuilder<FantasyGroup> builder)
    {
        builder.ToTable("FantasyGroups", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_FantasyGroups_Price", "([Type] <> 2 AND [MonthlyPrice] IS NULL) OR ([Type] = 2 AND [MonthlyPrice] > 0)");
            t.HasCheckConstraint("CK_FantasyGroups_Type", "[Type] BETWEEN 0 AND 2");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(60);
        builder.Property(x => x.InviteCode).HasMaxLength(10).IsUnicode(false);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.IsHidden).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.MonthlyPrice).HasPrecision(12, 2);
        builder.Property(x => x.PrizeText).HasMaxLength(200);
        builder.Property(x => x.PrizeValue).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasIndex(x => x.InviteCode).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_FantasyGroups_InviteCode");
        builder.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Sponsor).WithMany().HasForeignKey(x => x.SponsorId).OnDelete(DeleteBehavior.NoAction);
    }
}
