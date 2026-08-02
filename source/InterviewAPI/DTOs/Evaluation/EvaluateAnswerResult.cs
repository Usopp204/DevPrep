namespace InterviewAPI.DTOs.Evaluation;

public sealed class EvaluateAnswerResult
{
    public Guid EvaluationId { get; set; }
    public Guid SessionId { get; set; }
    public Guid SessionQuestionId { get; set; }
    public decimal Score { get; set; }
}