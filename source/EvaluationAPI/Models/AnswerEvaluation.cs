using System.ComponentModel.DataAnnotations;

namespace EvaluationAPI.Models;

public class AnswerEvaluation
{
    [Key]
    public Guid EvaluationId { get; set; }

    public Guid SessionId { get; set; }

    public Guid SessionQuestionId { get; set; }

    public string? QuestionContent { get; set; }

    public string? UserAnswer { get; set; }

    public decimal Score { get; set; }

    public string? CriteriaScoresJson { get; set; }

    public string? Strengths { get; set; }

    public string? Weaknesses { get; set; }

    public string? AiFeedback { get; set; }

    public string? ModelUsed { get; set; }

    public DateTime GeneratedAt { get; set; }
}
