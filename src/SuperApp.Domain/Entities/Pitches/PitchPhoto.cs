using SuperApp.Domain.Common;
using SuperApp.Domain.Entities.Ops;

namespace SuperApp.Domain.Entities.Pitches;

/// <summary>A photo of a pitch. Table <c>pitch.PitchPhotos</c>.</summary>
public class PitchPhoto : BaseEntity
{
    public long PitchId { get; set; }
    public long FileId { get; set; }
    public int SortOrder { get; set; }

    // Navigation
    public Pitch Pitch { get; set; } = null!;
    public StoredFile File { get; set; } = null!;
}
