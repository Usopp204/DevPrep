namespace InterviewAPI.DTOs.Evaluation;

// Body sent to EvaluationAPI POST /api/evaluation/sessions/{id}/answers/{answerId}.
// Mirrors EvaluationAPI.DTOs.Evaluation.EvaluateAnswerRequest.
public class EvaluateAnswerPayload
{
    public string Question { get; set; } = string.Empty;
    public string ReferenceAnswer { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Difficulty { get; set; } = string.Empty;
    public string UserAnswer { get; set; } = string.Empty;
}
