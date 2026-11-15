using SuperApp.Domain.Common;
using SuperApp.Domain.Enums;

namespace SuperApp.Domain.Entities.Communities;

/// <summary>A community (forum) created by Super App. Table <c>community.Communities</c>.</summary>
public class Community : BaseEntity, IHasAuthor
{
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? About { get; set; }
    public CommunityAudience Audience { get; set; }
    public string? RulesText { get; set; }
    public int SlowModeSeconds { get; set; }
    public string? Color { get; set; }
    public string? Icon { get; set; }
    public bool IsArchived { get; set; }
    public Guid? CreatedById { get; set; }

    // Navigation
    public ICollection<CommunityMember> Members { get; set; } = [];
}
