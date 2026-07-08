namespace QuestionAPI.DTOs;

public class AdminQuestionListItemDto
{
    public Guid QuestionId { get; set; }

    public Guid? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Content { get; set; } = string.Empty;

    public string QuestionType { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Difficulty { get; set; } = string.Empty;

    public string? TechStack { get; set; }

    public string? Tags { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public Guid? DeletedBy { get; set; }

    public DateTime CreatedAt { get; set; }
}