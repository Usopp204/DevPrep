namespace EvaluationAPI.DTOs.Evaluation;

public class EvaluateAnswerRequest
{
    public string Question { get; set; } = string.Empty;
    public string ReferenceAnswer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string UserAnswer { get; set; } = string.Empty;
}
