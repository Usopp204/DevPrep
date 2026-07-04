namespace WebClient.Models.Manager;

public class QuestionBankDetailViewModel
{
    public Guid QuestionId { get; set; }
    public string? CategoryName { get; set; }
    public string Content { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? ExpectedAnswer { get; set; }
    public string? TechStack { get; set; }
    public string? Tags { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
