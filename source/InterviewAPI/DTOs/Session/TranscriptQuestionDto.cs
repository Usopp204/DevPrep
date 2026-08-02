namespace InterviewAPI.DTOs.Session;

public class TranscriptQuestionDto
{
    public Guid SessionQuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool HasSubstantiveAnswer { get; set; }
    public string? Category { get; set; }
    public string? Difficulty { get; set; }
}
