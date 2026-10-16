using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A pop-up ad with a 3D object. Table <c>content.PopupAds</c>.</summary>
public class PopupAd : BaseEntity
{
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string TextEn { get; set; } = string.Empty;
    public string TextAr { get; set; } = string.Empty;
    public string CtaEn { get; set; } = string.Empty;
    public string CtaAr { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string? Badge { get; set; }
    public string Color { get; set; } = string.Empty;
    public PopupShape Shape { get; set; }
    public string? Icons { get; set; }
    public bool HideFromSellers { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public int ShownCount { get; set; }
    public int ClickedCount { get; set; }
}
