namespace EvaluationAPI.Services;

public sealed class EvaluationRequestException : Exception
{
    public EvaluationRequestException() : base("The grading request is invalid.") { }
}

public sealed class EvaluationDependencyException : Exception
{
    public EvaluationDependencyException(Exception? innerException = null)
        : base("Evaluation is temporarily unavailable.", innerException) { }
}

public sealed class EvaluationPersistenceException : Exception
{
    public EvaluationPersistenceException(Exception innerException)
        : base("Evaluation persistence is temporarily unavailable.", innerException) { }
}