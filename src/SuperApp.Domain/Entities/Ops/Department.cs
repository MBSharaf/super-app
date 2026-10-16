using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Ops;

/// <summary>A company department for the org chart. Table <c>ops.Departments</c>.</summary>
public class Department : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Color { get; set; } = "#0B6E4F";
}
