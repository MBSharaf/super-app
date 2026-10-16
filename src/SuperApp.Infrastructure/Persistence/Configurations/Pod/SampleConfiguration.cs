using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Pod;
using SuperApp.Domain.Entities.Shop;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pod;

/// <summary>Table <c>pod.Samples</c> — see ERD.</summary>
internal sealed class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable("Samples", "pod", t =>
        {
            t.HasCheckConstraint("CK_Samples_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Samples_Status", "[Status] BETWEEN 0 AND 4");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Color).HasMaxLength(40);
        builder.Property(x => x.Size).HasMaxLength(40);
        builder.Property(x => x.Address).HasMaxLength(400);
        builder.Property(x => x.Price).HasPrecision(12, 2);
        builder.Property(x => x.IsFree).HasDefaultValue(false).ValueGeneratedNever();
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(SampleStatus.Requested).ValueGeneratedNever();
        builder.HasOne(x => x.Seller).WithMany().HasForeignKey(x => x.SellerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Design).WithMany().HasForeignKey(x => x.DesignId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.NoAction);
    }
}
