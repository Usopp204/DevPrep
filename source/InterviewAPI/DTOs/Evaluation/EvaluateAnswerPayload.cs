namespace InterviewAPI.DTOs.Evaluation;

// Body sent to EvaluationAPI POST /api/evaluation/sessions/{id}/answers/{answerId}.
// Mirrors EvaluationAPI.DTOs.Evaluation.EvaluateAnswerRequest.
public sealed record EvaluateAnswerPayload
{
    public string Question { get; init; } = string.Empty;
    public string ReferenceAnswer { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Difficulty { get; init; } = string.Empty;
    public string UserAnswer { get; init; } = string.Empty;
}