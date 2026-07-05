namespace InterviewAPI.DTOs.Session;

public class NextQuestionDto
{
    public Guid SessionQuestionId { get; set; }
    public int OrderIndex { get; set; }
    public string Content { get; set; } = string.Empty;
}
