using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Content;

/// <summary>Terms, privacy, refunds, waiver, agreements, cookies. Table <c>content.LegalDocuments</c>.</summary>
public class LegalDocument : LookupEntity
{
    public string Key { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public bool RequiresAcceptance { get; set; } = true;

    // Navigation
    public ICollection<LegalVersion> Versions { get; set; } = [];
}
