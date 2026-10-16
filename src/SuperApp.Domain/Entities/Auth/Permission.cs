using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Auth;

/// <summary>A fine-grained permission code such as payouts.send. Table <c>auth.Permissions</c>.</summary>
public class Permission : LookupEntity
{
    public string Code { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? DescriptionEn { get; set; }
    public bool IsSensitive { get; set; }
}
