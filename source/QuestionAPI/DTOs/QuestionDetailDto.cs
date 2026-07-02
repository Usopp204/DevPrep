namespace QuestionAPI.DTOs;

public class QuestionDetailDto
{
    public Guid QuestionId { get; set; }
    public QuestionCategoryDto? Category { get; set; }
    public string Content { get; set; } = string.Empty;
    public string QuestionType { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string? ExpectedAnswer { get; set; }
    public string? TechStack { get; set; }
    public string? Tags { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
