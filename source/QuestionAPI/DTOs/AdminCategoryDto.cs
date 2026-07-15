namespace QuestionAPI.DTOs;

public class AdminCategoryDto
{
    public Guid CategoryId { get; set; }

    public string Category { get; set; } = string.Empty; //đổi name 
}