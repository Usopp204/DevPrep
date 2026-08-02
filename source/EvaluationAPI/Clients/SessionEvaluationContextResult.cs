namespace EvaluationAPI.Clients;

public sealed class SessionEvaluationContextResult
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? TerminationReason { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public int TotalPausedSeconds { get; set; }
    public List<SessionEvaluationQuestionResult> Questions { get; set; } = new();
}

public sealed class SessionEvaluationQuestionResult
{
    public Guid SessionQuestionId { get; set; }
    public bool HasSubstantiveAnswer { get; set; }
    public string? Category { get; set; }
    public string? Difficulty { get; set; }
}
