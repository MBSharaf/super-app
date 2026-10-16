using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A home page banner. Table <c>content.Banners</c>.</summary>
public class Banner : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Badge { get; set; }
    public string? CtaText { get; set; }
    public string Link { get; set; } = string.Empty;
    public string ImageKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateOnly? ShowFrom { get; set; }
    public DateOnly? ShowUntil { get; set; }
}
