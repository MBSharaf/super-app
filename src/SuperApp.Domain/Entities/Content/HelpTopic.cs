using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A help center topic. Table <c>content.HelpTopics</c>.</summary>
public class HelpTopic : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
}
