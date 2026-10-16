using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Fantasy;

/// <summary>A football club (from API-FOOTBALL). Table <c>fantasy.Clubs</c>.</summary>
public class Club : BaseEntity
{
    public int ExternalId { get; set; }
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string KitColor { get; set; } = "#0D1411";
    public string KitColor2 { get; set; } = "#FFFFFF";
    public string? LogoUrl { get; set; }
}
