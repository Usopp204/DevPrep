namespace EvaluationAPI.DTOs.Evaluation;

public class EvaluateAnswerResponse
{
    public Guid EvaluationId { get; set; }
    public Guid SessionId { get; set; }
    public Guid SessionQuestionId { get; set; }
    public decimal Score { get; set; }
    public string? CriteriaScoresJson { get; set; }
    public string? Strengths { get; set; }
    public string? AiFeedback { get; set; }
    public string? ModelUsed { get; set; }
}
