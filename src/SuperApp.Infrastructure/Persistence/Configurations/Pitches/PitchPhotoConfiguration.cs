using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Entities.Pitches;

namespace SuperApp.Infrastructure.Persistence.Configurations.Pitches;

/// <summary>Table <c>pitch.PitchPhotos</c> — see ERD.</summary>
internal sealed class PitchPhotoConfiguration : IEntityTypeConfiguration<PitchPhoto>
{
    public void Configure(EntityTypeBuilder<PitchPhoto> builder)
    {
        builder.ToTable("PitchPhotos", "pitch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SortOrder).HasDefaultValue(0).ValueGeneratedNever();
        builder.HasOne(x => x.Pitch).WithMany(p => p.Photos).HasForeignKey(x => x.PitchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.NoAction);
    }
}
