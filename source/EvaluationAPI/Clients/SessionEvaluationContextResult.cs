namespace EvaluationAPI.Clients;

public sealed class SessionEvaluationContextResult
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<SessionEvaluationQuestionResult> Questions { get; set; } = new();
}

public sealed class SessionEvaluationQuestionResult
{
    public Guid SessionQuestionId { get; set; }
    public bool HasSubstantiveAnswer { get; set; }
}
