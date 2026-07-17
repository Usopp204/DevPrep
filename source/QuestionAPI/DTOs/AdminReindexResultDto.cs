namespace QuestionAPI.DTOs;

public class AdminReindexResultDto
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int FailCount { get; set; }
}