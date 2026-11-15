using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>A word blocked in posts and comments. Table <c>community.BannedWords</c>.</summary>
public class BannedWord : BaseEntity
{
    public string Word { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
}
