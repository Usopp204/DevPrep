namespace WebClient.Models.Manager;

public class QuestionBankListItemViewModel
{
    public Guid QuestionId { get; set; }
    public Guid? CategoryId { get; set; }
    public string? Category { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuestionBankCategoryViewModel
{
    public Guid CategoryId { get; set; }
    public string Category { get; set; } = string.Empty;
}
