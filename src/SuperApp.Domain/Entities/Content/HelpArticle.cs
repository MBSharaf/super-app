using SuperApp.Domain.Common;

namespace SuperApp.Domain.Entities.Content;

/// <summary>A help center question and answer. Table <c>content.HelpArticles</c>.</summary>
public class HelpArticle : BaseEntity
{
    public int TopicId { get; set; }
    public string QuestionEn { get; set; } = string.Empty;
    public string QuestionAr { get; set; } = string.Empty;
    public string AnswerEn { get; set; } = string.Empty;
    public string AnswerAr { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }
    public bool IsPublished { get; set; } = true;

    // Navigation
    public HelpTopic Topic { get; set; } = null!;
    public ICollection<HelpVote> Votes { get; set; } = [];
}
