using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Auth;
using SuperApp.Domain.Entities.Ops;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A seller's design on a printable product (front and/or back artwork). Table <c>pod.Designs</c>.</summary>
public class Design : BaseEntity, ISoftDelete, IHasRowVersion
{
    public long SellerId { get; set; }
    public int CatalogProductId { get; set; }
    public int? CategoryId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public long? FrontArtFileId { get; set; }
    public long? BackArtFileId { get; set; }
    public decimal? FrontX { get; set; }
    public decimal? FrontY { get; set; }
    public decimal? FrontW { get; set; }
    public decimal? BackX { get; set; }
    public decimal? BackY { get; set; }
    public decimal? BackW { get; set; }
    public DesignStatus Status { get; set; }
    public string? RejectReason { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedById { get; set; }
    public bool SpotCheck { get; set; }
    public int Views { get; set; }
    public int Sales { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Seller Seller { get; set; } = null!;
    public CatalogProduct CatalogProduct { get; set; } = null!;
    public DesignCategory? Category { get; set; }
    public StoredFile? FrontArtFile { get; set; }
    public StoredFile? BackArtFile { get; set; }
    public User? ReviewedBy { get; set; }
    public ICollection<DesignTag> Tags { get; set; } = [];
    public ICollection<DesignColor> Colors { get; set; } = [];
}
