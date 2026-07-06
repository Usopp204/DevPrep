namespace QuestionAPI.DTOs;

public class AdminQuestionQueryDto
{
    public string? Keyword { get; set; }

    public string? QuestionType { get; set; }

    public string? Level { get; set; }

    public string? Difficulty { get; set; }

    public string? TechStack { get; set; }

    public Guid? CategoryId { get; set; }

    public string? Status { get; set; }

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}