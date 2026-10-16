using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Infrastructure.Persistence.Configurations.Ops;

/// <summary>Table <c>ops.Files</c> — see ERD.</summary>
internal sealed class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("Files", "ops", t =>
        {
            t.HasCheckConstraint("CK_Files_ContentType", "[ContentType] IN ('image/png', 'image/jpeg', 'image/webp', 'image/svg+xml', 'application/pdf', 'model/gltf-binary')");
            t.HasCheckConstraint("CK_Files_SizeBytes", "[SizeBytes] > 0 AND [SizeBytes] <= 20971520");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StorageKey).HasMaxLength(400);
        builder.Property(x => x.Container).HasMaxLength(40).IsUnicode(false);
        builder.Property(x => x.FileName).HasMaxLength(200);
        builder.Property(x => x.ContentType).HasMaxLength(100).IsUnicode(false);
        builder.Property(x => x.Sha256).HasMaxLength(64).IsUnicode(false).IsFixedLength();
        builder.Property(x => x.IsPrivate).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasIndex(x => x.StorageKey).IsUnique().HasFilter("[IsDeleted] = 0").HasDatabaseName("UQ_Files_StorageKey");
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
