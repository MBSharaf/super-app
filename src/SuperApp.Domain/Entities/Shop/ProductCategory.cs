using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Shop;

/// <summary>A shop category. Table <c>shop.ProductCategories</c>.</summary>
public class ProductCategory : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
}
