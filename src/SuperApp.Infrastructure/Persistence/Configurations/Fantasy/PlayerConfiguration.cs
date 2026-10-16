using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Enums;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Players</c> — see ERD.</summary>
internal sealed class PlayerConfiguration : IEntityTypeConfiguration<Player>
{
    public void Configure(EntityTypeBuilder<Player> builder)
    {
        builder.ToTable("Players", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_Players_Position", "[Position] BETWEEN 0 AND 3");
            t.HasCheckConstraint("CK_Players_Price", "[Price] BETWEEN 3.5 AND 15.0");
            t.HasCheckConstraint("CK_Players_Status", "[Status] BETWEEN 0 AND 3");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100);
        builder.Property(x => x.NameAr).HasMaxLength(100);
        builder.Property(x => x.Position).HasConversion<byte>();
        builder.Property(x => x.Price).HasPrecision(4, 1);
        builder.Property(x => x.PhotoUrl).HasMaxLength(300);
        builder.Property(x => x.Status).HasConversion<byte>().HasDefaultValue(PlayerStatus.Available).ValueGeneratedNever();
        builder.Property(x => x.SelectedByPercent).HasPrecision(5, 2).HasDefaultValue(0m).ValueGeneratedNever();
        builder.HasIndex(x => x.ExternalId).IsUnique().HasDatabaseName("UQ_Players_ExternalId");
        builder.HasIndex(x => new { x.ClubId, x.Position }).HasDatabaseName("IX_Players_ClubId_Position");
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.NoAction);
    }
}
