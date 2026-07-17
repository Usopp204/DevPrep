namespace QuestionAPI.DTOs.TechStacks;

public class TechStackDto
{
    public Guid TechStackId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
