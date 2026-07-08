namespace CvJdAPI.Models;

public class JobDescription
{
    public Guid JdId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Position { get; set; }
    public string? Level { get; set; }
    public string RawText { get; set; } = string.Empty;
    public string? ClarifiedJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
