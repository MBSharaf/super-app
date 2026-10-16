using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Money;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Money;

/// <summary>Table <c>money.PromoCodes</c> — see ERD.</summary>
internal sealed class PromoCodeConfiguration : IEntityTypeConfiguration<PromoCode>
{
    public void Configure(EntityTypeBuilder<PromoCode> builder)
    {
        builder.ToTable("PromoCodes", "money", t =>
        {
            t.HasCheckConstraint("CK_PromoCodes_Owner", "[Funding] <> 0 OR [OwnerUserId] IS NOT NULL");
            t.HasCheckConstraint("CK_PromoCodes_Percent", "[DiscountType] <> 0 OR [Value] <= 100");
            t.HasCheckConstraint("CK_PromoCodes_Dates", "[EndsOn] IS NULL OR [EndsOn] >= [StartsOn]");
            t.HasCheckConstraint("CK_PromoCodes_Code", "[Code] NOT LIKE '%[^A-Z0-9]%'");
            t.HasCheckConstraint("CK_PromoCodes_Funding", "[Funding] BETWEEN 0 AND 1");
            t.HasCheckConstraint("CK_PromoCodes_Scope", "[Scope] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_PromoCodes_DiscountType", "[DiscountType] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_PromoCodes_Value", "[Value] >= 0");
            t.HasCheckConstraint("CK_PromoCodes_MinSpend", "[MinSpend] >= 0");
            t.HasCheckConstraint("CK_PromoCodes_MaxUses", "[MaxUses] IS NULL OR [MaxUses] > 0");
            t.HasCheckConstraint("CK_PromoCodes_PerCustomer", "[PerCustomer] IS NULL OR [PerCustomer] > 0");
            t.HasCheckConstraint("CK_PromoCodes_UsesCount", "[UsesCount] >= 0");
            t.HasCheckConstraint("CK_PromoCodes_Status", "[Status] BETWEEN 0 AND 1");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(30).IsUnicode(false);
        builder.Property(x => x.Funding).HasConversion<byte>();
        builder.Property(x => x.Scope).HasConversion<byte>();
        builder.Property(x => x.DiscountType).HasConversion<byte>();
        builder.Property(x => x.Value).HasPrecision(12, 2);
        builder.Property(x => x.MinSpend).HasPrecision(12, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.Property(x => x.UsesCount).HasDefaultValue(0).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(CodeStatus.Active).ValueGeneratedNever();
        builder.Property(x => x.Label).HasMaxLength(120);
        builder.Property(x => x.Note).HasMaxLength(120);
        builder.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_PromoCodes_Code");
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.NoAction);
    }
}
