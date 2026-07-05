namespace QuestionAPI.DTOs;

public class AdminUpdateQuestionDto
{
    public Guid? CategoryId { get; set; }

    public string? Content { get; set; }

    public string? QuestionType { get; set; }

    public string? Level { get; set; }

    public string? Difficulty { get; set; }

    public string? ExpectedAnswer { get; set; }

    public string? TechStack { get; set; }

    public string? Tags { get; set; }

    public string? Status { get; set; }
}