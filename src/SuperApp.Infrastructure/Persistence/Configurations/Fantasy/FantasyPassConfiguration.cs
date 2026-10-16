using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Fantasy;
using SuperApp.Domain.Entities.Money;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.FantasyPasses</c> — see ERD.</summary>
internal sealed class FantasyPassConfiguration : IEntityTypeConfiguration<FantasyPass>
{
    public void Configure(EntityTypeBuilder<FantasyPass> builder)
    {
        builder.ToTable("FantasyPasses", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_FantasyPasses_Dates", "[EndsAt] > [StartsAt]");
            t.HasCheckConstraint("CK_FantasyPasses_Price", "[Price] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Price).HasPrecision(12, 2);
        builder.HasIndex(x => new { x.UserId, x.EndsAt }).HasDatabaseName("IX_FantasyPasses_UserId_EndsAt");
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.NoAction);
    }
}
