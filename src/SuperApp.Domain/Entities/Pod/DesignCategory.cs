using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Pod;

/// <summary>A design section (football, anime…). Table <c>pod.DesignCategories</c>.</summary>
public class DesignCategory : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
}
