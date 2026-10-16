using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Fantasy;

namespace SuperApp.Infrastructure.Persistence.Configurations.Fantasy;

/// <summary>Table <c>fantasy.Seasons</c> — see ERD.</summary>
internal sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("Seasons", "fantasy", t =>
        {
            t.HasCheckConstraint("CK_Seasons_Year", "[Year] BETWEEN 2020 AND 2100");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IsCurrent).HasDefaultValue(false).ValueGeneratedNever();
        builder.HasIndex(x => new { x.CompetitionId, x.Year }).IsUnique().HasDatabaseName("UQ_Seasons_CompetitionId_Year");
        builder.HasIndex(x => x.CompetitionId).IsUnique().HasFilter("[IsCurrent] = 1").HasDatabaseName("UQ_Seasons_OneCurrent");
        builder.HasOne(x => x.Competition).WithMany().HasForeignKey(x => x.CompetitionId).OnDelete(DeleteBehavior.NoAction);
    }
}
