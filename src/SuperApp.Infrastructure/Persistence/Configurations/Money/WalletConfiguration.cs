using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.Wallets</c> — see ERD.</summary>
internal sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets", "money", t =>
        {
            t.HasCheckConstraint("CK_Wallets_AvailableBalance", "[AvailableBalance] >= 0 OR [Type] >= 5");
            t.HasCheckConstraint("CK_Wallets_Type", "[Type] BETWEEN 0 AND 6");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<byte>();
        builder.Property(x => x.Currency).HasMaxLength(3).IsUnicode(false).IsFixedLength().HasDefaultValue("EGP").ValueGeneratedNever();
        builder.Property(x => x.PendingBalance).HasPrecision(14, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.AvailableBalance).HasPrecision(14, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.SettledTotal).HasPrecision(14, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasIndex(x => new { x.OwnerUserId, x.Type }).IsUnique().HasDatabaseName("UQ_Wallets_OwnerUserId_Type");
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
